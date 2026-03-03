import { createRouter, createWebHistory } from 'vue-router';
import AuthView from '../views/AuthView.vue';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { 
      path: '/', 
      name: 'auth',
      component: AuthView 
    },
    { 
      path: '/chat', 
      name: 'chat',
      component: () => import('../views/ChatView.vue') 
    }
  ]
});

export default router;