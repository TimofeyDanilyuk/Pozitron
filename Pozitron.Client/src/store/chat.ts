import { defineStore } from 'pinia';
import api from '../api';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from './auth';

interface Message {
  id: string;
  chatId: string;
  content: string;
  sentAt: string;
  userId: string;
  username: string;
  avatarUrl?: string;
  emojiPrefix?: string;
}

interface Chat {
  id: string;
  type: number; // 0 = General, 1 = Direct
  name?: string;
  avatarUrl?: string;
  lastMessage?: string;
  unreadCount: number;
}

export const useChatStore = defineStore('chat', {
  state: () => ({
    chats: [] as Chat[],
    messages: {} as Record<string, Message[]>,
    activeChat: null as Chat | null,
    onlineUsers: [] as string[],
    connection: null as signalR.HubConnection | null,
    users: [] as any[],
  }),

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

      // Входящее сообщение
      this.connection.on('ReceiveMessage', (msg: Message) => {
        if (!this.messages[msg.chatId]) this.messages[msg.chatId] = [];
        // Защита от дублей
        const already = this.messages[msg.chatId]!.find(m => m.id === msg.id);
        if (!already) this.messages[msg.chatId]!.push(msg);
        // Обновляем превью последнего сообщения
        const chat = this.chats.find(c => c.id === msg.chatId);
        if (chat) chat.lastMessage = msg.content;
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

      // Новый DM чат — добавляем в список
      this.connection.on('NewDmChat', (chat: Chat) => {
        if (!this.chats.find(c => c.id === chat.id)) {
          this.chats.push(chat);
        }
      });

      // Обновление счётчика непрочитанных
      this.connection.on('UnreadUpdated', ({ chatId, unreadCount }: { chatId: string, unreadCount: number }) => {
        const chat = this.chats.find(c => c.id === chatId);
        if (chat) chat.unreadCount = unreadCount;
      });

      // После reconnect — переподключаемся ко всем чатам
      this.connection.onreconnected(async () => {
        await this._joinAllChats();
        if (this.activeChat) {
          const { data } = await api.get(`/chat/${this.activeChat.id}/messages`);
          this.messages[this.activeChat.id] = data;
        }
      });

      await this.connection.start();

      // Joinим все чаты сразу — чтобы получать сообщения из всех
      await this._joinAllChats();

      // Переподключение когда возвращаемся на вкладку
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

    // Joinим все чаты пользователя чтобы получать сообщения из всех
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
      await api.post(`/chat/${chat.id}/read`).catch(() => {});
      if (!this.messages[chat.id]) {
        const { data } = await api.get(`/chat/${chat.id}/messages`);
        this.messages[chat.id] = data;
      }
    },

    async sendMessage(content: string) {
      if (!this.activeChat || !content.trim()) return;
      await this.connection?.invoke('SendMessage', this.activeChat.id, content);
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