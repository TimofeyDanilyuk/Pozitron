<script setup lang="ts">
import { ref } from 'vue';
import { useAuthStore } from '../store/auth.ts'; 
import { useRouter } from 'vue-router';

const auth = useAuthStore();
const router = useRouter();

const isLogin = ref(true);
const username = ref('');
const password = ref('');
const error = ref('');
const loading = ref(false);

const handleSubmit = async () => {
  error.value = '';
  loading.value = true;
  
  try {
    if (isLogin.value) {
      await auth.login({ username: username.value, password: password.value });
      router.push('/chat'); 
    } else {
      await auth.register({ username: username.value, password: password.value });
      isLogin.value = true;
      alert('Регистрация прошла успешно! Теперь войдите в аккаунт.');
    }
  } catch (e: any) {
    error.value = e.response?.data || 'Ошибка сервера. Попробуйте позже.';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-linear-to-br from-indigo-500 via-purple-500 to-pink-500 p-4">
    
    <div class="bg-white/90 dark:bg-slate-800/90 backdrop-blur-md p-8 rounded-3xl shadow-2xl w-full max-w-md border border-white/20">
      
      <div class="flex justify-center mb-8 pointer-events-none select-none">
        <img 
          src="../assets/logo.png" 
          alt="Pozitron Logo"
          draggable="false"
          class="w-95 h-auto object-contain outline-hidden active:outline-hidden focus:outline-hidden"
        >
      </div>

      <div v-if="error" class="mb-4 p-3 bg-red-100 border-l-4 border-red-500 text-red-700 text-sm rounded">
        {{ error }}
      </div>

      <form @submit.prevent="handleSubmit" class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Username</label>
          <input v-model="username" type="text" required
            class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
            placeholder="Введите ник...">
        </div>

        <div>
          <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Password</label>
          <input v-model="password" type="password" required
            class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
            placeholder="••••••••">
        </div>

        <button type="submit" :disabled="loading"
          class="w-full bg-linear-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-bold py-3 rounded-xl shadow-lg transform active:scale-95 transition-all mt-4 disabled:opacity-50">
          <span v-if="loading">Секунду...</span>
          <span v-else>{{ isLogin ? 'Войти' : 'Создать аккаунт' }}</span>
        </button>
      </form>

      <div class="mt-6 text-center">
        <button @click="isLogin = !isLogin" class="text-purple-600 dark:text-purple-400 text-sm hover:underline font-medium">
          {{ isLogin ? 'Нет аккаунта? Зарегистрироваться' : 'Уже есть аккаунт? Войти' }}
        </button>
      </div>

    </div>
  </div>
</template>