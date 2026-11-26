import { defineStore } from 'pinia';
import api from '../api';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from './auth';

export interface Reaction {
  emoji: string;
  count: number;
  userIds: string[];
}

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
  isPinned?: boolean;
  replyToMessageId?: string;
  replyToContent?: string;
  replyToUsername?: string;
  reactions: Reaction[];
}

export interface Chat {
  id: string;
  type: number;
  name?: string;
  avatarUrl?: string;
  lastMessage?: string;
  unreadCount: number;
  isPinned?: boolean;
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
    replyingTo: null as Message | null,
  }),

  getters: {
    contactChats: (state) => state.chats.filter(c => c.type === 1 && c.isContact),
    otherChats: (state) => state.chats.filter(c => c.type === 0 || (c.type === 1 && !c.isContact)),
  },

  actions: {
    async connect() {
      const auth = useAuthStore();
      const token = auth.token;

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('http://localhost:5088/hubs/chat', {
          accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .build();

      this.connection.on('ReceiveMessage', (msg: Message) => {
        if (!this.messages[msg.chatId]) this.messages[msg.chatId] = [];
        const already = this.messages[msg.chatId]!.find(m => m.id === msg.id);
        if (!already) {
          if (!msg.reactions) msg.reactions = [];
          this.messages[msg.chatId]!.push(msg);
        }
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

      this.connection.on('ReactionUpdated', ({ messageId, reactions }: { messageId: string, reactions: Reaction[] }) => {
        for (const msgs of Object.values(this.messages)) {
          const msg = msgs.find(m => m.id.toLowerCase() === messageId.toLowerCase());
          if (msg) {
            msg.reactions = reactions;
            break;
          }
        }
      });

      this.connection.on('MessagesDeleted', ({ chatId, messageIds }: { chatId: string, messageIds: string[] }) => {
        const key = Object.keys(this.messages).find(k => k.toLowerCase() === chatId.toLowerCase());
        if (key) {
          this.messages[key] = this.messages[key]!.filter(
            m => !messageIds.some(id => id.toLowerCase() === m.id.toLowerCase())
          );
        }
      });

      this.connection.on('MessagePinned', ({ chatId, messageId, isPinned }: { chatId: string, messageId: string, isPinned: boolean }) => {
        const key = Object.keys(this.messages).find(k => k.toLowerCase() === chatId.toLowerCase());
        const msg = key ? this.messages[key]?.find(m => m.id.toLowerCase() === messageId.toLowerCase()) : undefined;
        if (msg) msg.isPinned = isPinned;
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
      this.replyingTo = null;
      await this.connection?.invoke('JoinChat', chat.id);
      if (!this.messages[chat.id]) {
        const { data } = await api.get(`/chat/${chat.id}/messages`);
        this.messages[chat.id] = data;
      }
      await api.post(`/chat/${chat.id}/read`).catch(() => {});
    },

    async loadChatMessages(chatId: string) {
      if (!this.messages[chatId]) {
        const { data } = await api.get(`/chat/${chatId}/messages`);
        this.messages[chatId] = data;
      }
      return this.messages[chatId];
    },

    async sendMessage(content: string, targetChatId?: string) {
      const chatId = targetChatId || this.activeChat?.id;
      if (!chatId || !content.trim()) return;
      const replyId = this.replyingTo?.id ?? null;
      await this.connection?.invoke('SendMessage', chatId, content, replyId);
      this.replyingTo = null;
    },

    async sendSticker(stickerId: string, targetChatId?: string) {
      const chatId = targetChatId || this.activeChat?.id;
      if (!chatId) return;
      await this.connection?.invoke('SendSticker', chatId, stickerId);
    },

    setReply(message: Message | null) {
      this.replyingTo = message;
    },

    async addReaction(messageId: string, emoji: string) {
      await this.connection?.invoke('AddReaction', messageId, emoji);
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
    },

    async uploadAttachment(file: File, targetChatId?: string) {
      const chatId = targetChatId || this.activeChat?.id;
      if (!chatId) return;
      const formData = new FormData();
      formData.append('file', file);
      if (this.replyingTo) {
        formData.append('replyToMessageId', this.replyingTo.id);
      }
      await api.post(`/chat/${chatId}/upload`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      this.replyingTo = null;
    },

    async deleteMessages(messageIds: string[]) {
      if (!this.activeChat) return;
      if (messageIds.length === 1) {
        await api.delete(`/chat/${this.activeChat.id}/messages/${messageIds[0]}`);
      } else {
        await api.delete(`/chat/${this.activeChat.id}/messages`, { data: messageIds });
      }
    },

    async togglePinMessage(messageId: string, targetChatId?: string) {
      const chatId = targetChatId || this.activeChat?.id;
      if (!chatId) return;
      await api.post(`/chat/${chatId}/messages/${messageId}/pin`);
    },

    async togglePinChat(chatId: string) {
      const { data } = await api.post(`/chat/${chatId}/pin`);
      const c = this.chats.find(x => x.id === chatId);
      if (c) c.isPinned = data.isPinned;
    },
  }
});