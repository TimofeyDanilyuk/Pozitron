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
      component: () => import('../views/ChatView.vue'),
      meta: { requiresAuth: true }
    }
  ]
});

router.beforeEach((to, _from, next) => {
  const token = localStorage.getItem('token');
  if (to.meta.requiresAuth && !token) {
    next({ name: 'auth' });
  } else if (to.name === 'auth' && token) {
    next({ name: 'chat' });
  } else {
    next();
  }
});

export default router;