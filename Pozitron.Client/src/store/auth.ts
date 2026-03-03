import { defineStore } from 'pinia';
import api from '../api'; 

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: JSON.parse(localStorage.getItem('user') || 'null'),
    token: localStorage.getItem('token') || '',
  }),
  
  getters: {
    isAuthenticated: (state) => !!state.token,
  },

  actions: {
    async login(credentials: any) {
      try {
        const { data } = await api.post('/auth/login', credentials);
        
        this.token = data.token;
        this.user = data.user;
        
        localStorage.setItem('token', data.token);
        localStorage.setItem('user', JSON.stringify(data.user));
      } catch (error) {
        throw error;
      }
    },

    async register(credentials: any) {
      try {
        await api.post('/auth/register', credentials);
      } catch (error) {
        throw error;
      }
    },

    logout() {
      this.token = '';
      this.user = null;
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    },

    async uploadAvatar(file: File) {
    const formData = new FormData();
    formData.append('file', file);

      try {
          const { data } = await api.post('/user/upload-avatar', formData, {
              headers: { 'Content-Type': 'multipart/form-data' }
          });
          if (this.user) {
              this.user.avatarUrl = data.avatarUrl;
              localStorage.setItem('user', JSON.stringify(this.user));
          }
            return data.avatarUrl;
          } catch (error) {
              throw error;
          }
    },

    async updateProfile(profileData: { emojiPrefix?: string, displayName?: string }) {
        try {
            const { data } = await api.patch('/user/profile', profileData);
            if (this.user) {
                this.user.emojiPrefix = data.emojiPrefix;
                this.user.displayName = data.displayName;
                localStorage.setItem('user', JSON.stringify(this.user));
            }
        } catch (error) {
            throw error;
        }
    },

    async changeUsername(username: string) {
    const { data } = await api.patch('/user/username', { username });
    if (this.user) {
        this.user.username = data.username;
        localStorage.setItem('user', JSON.stringify(this.user));
    }
}
  }
});