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
    }
  }
});