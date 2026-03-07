import { defineStore } from 'pinia';
import api from '../api';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from './auth';

export interface Message {
  id: string;
  chatId: string;
  content: string;
  attachmentUrl?: string;
  type?: string;
  packId?: string;
  sentAt: string;
  userId: string;
  username: string;
  avatarUrl?: string;
  emojiPrefix?: string;
  isRead?: boolean;
}

export interface Chat {
  id: string;
  type: number;
  name?: string;
  avatarUrl?: string;
  lastMessage?: string;
  unreadCount: number;
  isContact?: boolean;
}

export interface Sticker {
  id: string;
  url: string;
}

export interface StickerPack {
  id: string;
  name: string;
  coverUrl?: string;
  createdByMe: boolean;
  stickers: Sticker[];
}

export const useChatStore = defineStore('chat', {
  state: () => ({
    chats: [] as Chat[],
    messages: {} as Record<string, Message[]>,
    activeChat: null as Chat | null,
    onlineUsers: [] as string[],
    connection: null as signalR.HubConnection | null,
    users: [] as any[],
    allUsers: [] as any[],
    stickerPacks: [] as StickerPack[],
    stickerPacksLoaded: false,
  }),

  getters: {
    contactChats: (state) => state.chats.filter(c => c.type === 1 && c.isContact),
    otherChats: (state) => state.chats.filter(c => !c.isContact),
  },

  actions: {
    async connect() {
      const auth = useAuthStore();
      const token = auth.token;

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('https://pozitron-production.up.railway.app/hubs/chat', {
          accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .build();

      this.connection.on('ReceiveMessage', (msg: Message) => {
        if (!this.messages[msg.chatId]) this.messages[msg.chatId] = [];
        const already = this.messages[msg.chatId]!.find(m => m.id === msg.id);
        if (!already) this.messages[msg.chatId]!.push(msg);
        const chat = this.chats.find(c => c.id === msg.chatId);
        if (chat) chat.lastMessage = msg.type === 'Sticker' ? '🎭 Стикер' : msg.content;
      });

      this.connection.on('OnlineUsers', (userIds: string[]) => {
        this.onlineUsers = userIds;
      });

      this.connection.on('UserOnline', (userId: string) => {
        if (!this.onlineUsers.includes(userId)) this.onlineUsers.push(userId);
      });

      this.connection.on('UserOffline', (userId: string) => {
        this.onlineUsers = this.onlineUsers.filter(id => id !== userId);
      });

      this.connection.on('NewDmChat', (incomingChat: Chat) => {
        const existing = this.chats.find(c => c.id === incomingChat.id);
        if (!existing) {
          this.chats.push(incomingChat);
        } else if (incomingChat.isContact) {
          existing.isContact = true;
        }
      });

      this.connection.on('UnreadUpdated', ({ chatId, unreadCount }: { chatId: string, unreadCount: number }) => {
        const chat = this.chats.find(c => c.id === chatId);
        if (chat) chat.unreadCount = unreadCount;
      });

      this.connection.on('MessagesRead', ({ chatId }: { chatId: string }) => {
        const key = Object.keys(this.messages).find(k => k.toLowerCase() === chatId.toLowerCase());
        if (key) {
          const auth = useAuthStore();
          this.messages[key]!.forEach(m => {
            if (m.userId.toLowerCase() === auth.user?.id?.toLowerCase()) {
              m.isRead = true;
            }
          });
        }
      });

      this.connection.onreconnected(async () => {
        await this._joinAllChats();
        if (this.activeChat) {
          const { data } = await api.get(`/chat/${this.activeChat.id}/messages`);
          this.messages[this.activeChat.id] = data;
        }
      });

      await this.connection.start();
      await this._joinAllChats();

      document.addEventListener('visibilitychange', async () => {
        if (document.visibilityState === 'visible') {
          if (this.connection?.state === signalR.HubConnectionState.Disconnected) {
            await this.connection.start();
            await this._joinAllChats();
            await this.loadChats();
            if (this.activeChat) {
              const { data } = await api.get(`/chat/${this.activeChat.id}/messages`);
              this.messages[this.activeChat.id] = data;
            }
          }
        }
      });
    },

    async _joinAllChats() {
      for (const chat of this.chats) {
        await this.connection?.invoke('JoinChat', chat.id);
      }
    },

    async disconnect() {
      await this.connection?.stop();
    },

    async loadChats() {
      const { data } = await api.get('/chat');
      this.chats = data;
      await this._joinAllChats();
    },

    async openChat(chat: Chat) {
      if (this.activeChat?.id === chat.id) return;
      this.activeChat = chat;
      chat.unreadCount = 0;
      await this.connection?.invoke('JoinChat', chat.id);
      // Сначала загружаем сообщения, потом помечаем прочитанными
      if (!this.messages[chat.id]) {
        const { data } = await api.get(`/chat/${chat.id}/messages`);
        this.messages[chat.id] = data;
      }
      await api.post(`/chat/${chat.id}/read`).catch(() => {});
    },

    async sendMessage(content: string) {
      if (!this.activeChat || !content.trim()) return;
      await this.connection?.invoke('SendMessage', this.activeChat.id, content);
    },

    async sendSticker(stickerId: string) {
      if (!this.activeChat) return;
      await this.connection?.invoke('SendSticker', this.activeChat.id, stickerId);
    },

    // ===== КОНТАКТЫ =====
    async loadAllUsers() {
      const { data } = await api.get('/chat/users', { params: { search: '' } });
      this.allUsers = data;
    },

    async addContact(contactId: string) {
      await api.post(`/user/contacts/${contactId}`);
      await this.loadChats();
    },

    async removeContact(contactId: string) {
      await api.delete(`/user/contacts/${contactId}`);
      await this.loadChats();
    },

    // ===== СТИКЕРЫ =====
    async loadMyPacks() {
      const { data } = await api.get('/sticker/my');
      this.stickerPacks = data;
      this.stickerPacksLoaded = true;
    },

    async createPack(name: string): Promise<StickerPack> {
      const { data } = await api.post('/sticker', { name });
      const newPack: StickerPack = { ...data, stickers: [], createdByMe: true };
      this.stickerPacks.push(newPack);
      return newPack;
    },

    async addStickerToPack(packId: string, file: File): Promise<Sticker> {
      const formData = new FormData();
      formData.append('file', file);
      const { data } = await api.post(`/sticker/${packId}/stickers`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      const pack = this.stickerPacks.find(p => p.id === packId);
      if (pack) {
        pack.stickers.push(data);
        if (!pack.coverUrl) pack.coverUrl = data.url;
      }
      return data;
    },

    async deleteStickerFromPack(packId: string, stickerId: string) {
      await api.delete(`/sticker/${packId}/stickers/${stickerId}`);
      const pack = this.stickerPacks.find(p => p.id === packId);
      if (pack) pack.stickers = pack.stickers.filter(s => s.id !== stickerId);
    },

    async getPack(packId: string) {
      const { data } = await api.get(`/sticker/${packId}`);
      return data;
    },

    async addPack(packId: string) {
      await api.post(`/sticker/${packId}/add`);
      await this.loadMyPacks();
    },

    async removePack(packId: string) {
      await api.delete(`/sticker/${packId}/remove`);
      this.stickerPacks = this.stickerPacks.filter(p => p.id !== packId);
    },

    async searchUsers(query: string) {
      const { data } = await api.get('/chat/users', { params: { search: query } });
      this.users = data;
    },

    async openDm(targetUserId: string) {
      const { data } = await api.post(`/chat/dm/${targetUserId}`);
      await this.loadChats();
      const chat = this.chats.find(c => c.id === data.id);
      if (chat) await this.openChat(chat);
    }
  }
});