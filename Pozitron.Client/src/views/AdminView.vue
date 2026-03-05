<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../store/auth';
import api from '../api';

const router = useRouter();
const auth = useAuthStore();

// Активная вкладка: 'stats' | 'users' | 'messages'
const activeTab = ref<'stats' | 'users' | 'messages'>('stats');
const loading = ref(false);

// Данные
const stats = ref<any>(null);
const users = ref<any[]>([]);
const messages = ref<any[]>([]);

// Модалка сброса пароля
const resetPasswordModal = ref(false);
const resetPasswordUser = ref<any>(null);
const newPassword = ref('');
const resetLoading = ref(false);

// Загрузка статистики
const loadStats = async () => {
  loading.value = true;
  try {
    const { data } = await api.get('/admin/stats');
    console.log('stats:', data);
    stats.value = data;
  } catch (e) {
    console.error('stats error:', e);
  } finally {
    loading.value = false;
  }
};

// Загрузка пользователей
const loadUsers = async () => {
  loading.value = true;
  try {
    const { data } = await api.get('/admin/users');
    users.value = data;
  } finally {
    loading.value = false;
  }
};

// Загрузка сообщений
const loadMessages = async () => {
  loading.value = true;
  try {
    const { data } = await api.get('/admin/messages');
    messages.value = data;
  } finally {
    loading.value = false;
  }
};

// Переключение вкладки
const switchTab = async (tab: 'stats' | 'users' | 'messages') => {
  activeTab.value = tab;
  if (tab === 'stats') await loadStats();
  if (tab === 'users') await loadUsers();
  if (tab === 'messages') await loadMessages();
};

// Удалить пользователя
const deleteUser = async (user: any) => {
  if (!confirm(`Удалить пользователя ${user.username}?`)) return;
  try {
    await api.delete(`/admin/users/${user.id}`);
    users.value = users.value.filter(u => u.id !== user.id);
  } catch (e: any) {
    alert(e.response?.data || 'Ошибка');
  }
};

// Заблокировать / разблокировать
const toggleBan = async (user: any) => {
  try {
    const { data } = await api.post(`/admin/users/${user.id}/ban`);
    user.isBanned = data.isBanned;
  } catch (e: any) {
    alert(e.response?.data || 'Ошибка');
  }
};

// Открыть модалку сброса пароля
const openResetPassword = (user: any) => {
  resetPasswordUser.value = user;
  newPassword.value = '';
  resetPasswordModal.value = true;
};

// Сбросить пароль
const submitResetPassword = async () => {
  if (!newPassword.value.trim()) return;
  resetLoading.value = true;
  try {
    await api.post(`/admin/users/${resetPasswordUser.value.id}/reset-password`, {
      newPassword: newPassword.value
    });
    resetPasswordModal.value = false;
  } catch (e: any) {
    alert(e.response?.data || 'Ошибка');
  } finally {
    resetLoading.value = false;
  }
};

// Удалить сообщение
const deleteMessage = async (messageId: string) => {
  if (!confirm('Удалить сообщение?')) return;
  try {
    await api.delete(`/admin/messages/${messageId}`);
    messages.value = messages.value.filter(m => m.id !== messageId);
  } catch (e: any) {
    alert(e.response?.data || 'Ошибка');
  }
};

const formatDate = (d: string) =>
  new Date(d).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' });

const handleLogout = () => {
  auth.logout();
  router.push('/');
};

onMounted(() => loadStats());
</script>

<template>
  <div class="min-h-screen bg-slate-900 text-slate-100 font-sans">

    <!-- Шапка -->
    <header class="bg-slate-950/80 backdrop-blur-md border-b border-slate-800 px-6 py-4 flex items-center justify-between">
      <div class="flex items-center gap-3">
        <!-- Кнопка назад в чат -->
        <button @click="router.push('/chat')"
                class="w-9 h-9 flex items-center justify-center rounded-xl hover:bg-slate-800 active:bg-slate-700 transition-all">
          <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
            <defs><linearGradient id="g" x1="0" y1="0" x2="24" y2="24">
            <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
            <path d="M15 6L9 12L15 18" stroke="url(#g)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </button>
        <div>
          <h1 class="text-lg font-black tracking-tight">⚙️ Админ панель</h1>
          <p class="text-xs text-slate-500">Pozitron</p>
        </div>
      </div>
      <button @click="handleLogout"
              class="px-3 py-2 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-400 text-xs font-bold transition-all border border-red-500/20 active:scale-95">
        Выйти
      </button>
    </header>

    <!-- Вкладки -->
    <div class="flex gap-1 px-6 pt-6">
      <button v-for="tab in [
        { key: 'stats', label: '📊 Статистика' },
        { key: 'users', label: '👥 Пользователи' },
        { key: 'messages', label: '💬 Сообщения' }
      ]" :key="tab.key"
        @click="switchTab(tab.key as any)"
        :class="[
          'px-4 py-2 rounded-xl text-sm font-bold transition-all',
          activeTab === tab.key
            ? 'bg-purple-600 text-white shadow-lg'
            : 'bg-slate-800 text-slate-400 hover:bg-slate-700'
        ]">
        {{ tab.label }}
      </button>
    </div>

    <div class="px-6 py-6">

      <!-- Загрузка -->
      <div v-if="loading" class="flex justify-center py-20 opacity-40">
        <div class="w-8 h-8 border-2 border-purple-500 border-t-transparent rounded-full animate-spin"></div>
      </div>

      <!-- ===== СТАТИСТИКА ===== -->
      <div v-if="!loading && activeTab === 'stats' && stats" class="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div v-for="item in [
          { label: 'Пользователей', value: stats.totalUsers, icon: '👤', color: 'from-purple-600 to-indigo-600' },
          { label: 'Сообщений', value: stats.totalMessages, icon: '💬', color: 'from-blue-600 to-cyan-600' },
          { label: 'Чатов', value: stats.totalChats, icon: '🗂️', color: 'from-green-600 to-teal-600' },
          { label: 'Заблокировано', value: stats.bannedUsers, icon: '🚫', color: 'from-red-600 to-orange-600' },
        ]" :key="item.label"
          class="bg-slate-800/50 border border-slate-700 rounded-2xl p-5">
          <div :class="['w-10 h-10 rounded-xl bg-gradient-to-br flex items-center justify-center text-xl mb-3', item.color]">
            {{ item.icon }}
          </div>
          <p class="text-2xl font-black">{{ item.value }}</p>
          <p class="text-xs text-slate-500 mt-1">{{ item.label }}</p>
        </div>
      </div>

      <!-- ===== ПОЛЬЗОВАТЕЛИ ===== -->
      <div v-if="!loading && activeTab === 'users'" class="space-y-3">
        <div v-for="user in users" :key="user.id"
             class="bg-slate-800/50 border border-slate-700 rounded-2xl p-4 flex items-center gap-4">

          <!-- Аватар -->
          <img v-if="user.avatarUrl" :src="user.avatarUrl" class="w-10 h-10 rounded-xl object-cover shrink-0">
          <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center font-bold text-sm shrink-0">
            {{ user.username?.[0]?.toUpperCase() }}
          </div>

          <!-- Инфо -->
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 flex-wrap">
              <span class="font-bold text-sm">{{ user.emojiPrefix }} {{ user.username }}</span>
              <!-- Бейдж роли -->
              <span v-if="user.role === 1"
                    class="px-2 py-0.5 bg-purple-600/30 text-purple-300 text-[10px] font-bold rounded-full border border-purple-500/30">
                ADMIN
              </span>
              <!-- Бейдж бана -->
              <span v-if="user.isBanned"
                    class="px-2 py-0.5 bg-red-600/30 text-red-300 text-[10px] font-bold rounded-full border border-red-500/30">
                БАН
              </span>
            </div>
            <p class="text-xs text-slate-500 mt-0.5">
              {{ user.messageCount }} сообщ. · {{ formatDate(user.createdAt) }}
            </p>
          </div>

          <!-- Кнопки действий — только для не-админов -->
          <div v-if="user.role !== 1" class="flex items-center gap-2 shrink-0">

            <!-- Сбросить пароль -->
            <button @click="openResetPassword(user)"
                    class="w-8 h-8 flex items-center justify-center rounded-lg bg-slate-700 hover:bg-blue-600/30 text-slate-400 hover:text-blue-300 transition-all active:scale-90"
                    title="Сбросить пароль">
              <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
              </svg>
            </button>

            <!-- Заблокировать / разблокировать -->
            <button @click="toggleBan(user)"
                    :class="['w-8 h-8 flex items-center justify-center rounded-lg transition-all active:scale-90',
                      user.isBanned
                        ? 'bg-green-600/20 hover:bg-green-600/40 text-green-400'
                        : 'bg-slate-700 hover:bg-orange-600/30 text-slate-400 hover:text-orange-300']"
                    :title="user.isBanned ? 'Разблокировать' : 'Заблокировать'">
              <svg v-if="!user.isBanned" xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
              </svg>
              <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </button>

            <!-- Удалить -->
            <button @click="deleteUser(user)"
                    class="w-8 h-8 flex items-center justify-center rounded-lg bg-slate-700 hover:bg-red-600/30 text-slate-400 hover:text-red-400 transition-all active:scale-90"
                    title="Удалить">
              <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>
      </div>

      <!-- ===== СООБЩЕНИЯ ===== -->
      <div v-if="!loading && activeTab === 'messages'" class="space-y-2">
        <div v-for="msg in messages" :key="msg.id"
             class="bg-slate-800/50 border border-slate-700 rounded-xl px-4 py-3 flex items-start gap-3">
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs font-bold text-purple-400">{{ msg.username }}</span>
              <span class="text-[10px] text-slate-600">в {{ msg.chatName }}</span>
              <span class="text-[10px] text-slate-600">· {{ formatDate(msg.sentAt) }}</span>
            </div>
            <p class="text-sm text-slate-300 break-words">{{ msg.content }}</p>
          </div>
          <!-- Удалить сообщение -->
          <button @click="deleteMessage(msg.id)"
                  class="w-7 h-7 flex items-center justify-center rounded-lg bg-slate-700 hover:bg-red-600/30 text-slate-500 hover:text-red-400 transition-all active:scale-90 shrink-0 mt-0.5">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>
      </div>

    </div>

    <!-- ===== МОДАЛКА СБРОСА ПАРОЛЯ ===== -->
    <Transition name="fade">
      <div v-if="resetPasswordModal" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4">
        <div @click="resetPasswordModal = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-slate-900 border border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl p-6 shadow-2xl">
          <h3 class="font-black text-lg mb-1">Сброс пароля</h3>
          <p class="text-sm text-slate-500 mb-4">Пользователь: <span class="text-white font-bold">{{ resetPasswordUser?.username }}</span></p>
          <input v-model="newPassword" type="text" placeholder="Новый пароль"
                 class="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-3 text-sm outline-none focus:ring-1 focus:ring-purple-500 mb-4 select-text">
          <div class="flex gap-3">
            <button @click="resetPasswordModal = false"
                    class="flex-1 bg-slate-800 hover:bg-slate-700 font-bold py-3 rounded-xl text-sm transition-all active:scale-95">
              Отмена
            </button>
            <button @click="submitResetPassword" :disabled="resetLoading || !newPassword.trim()"
                    class="flex-1 bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl text-sm shadow-lg active:scale-95 transition-all disabled:opacity-50">
              {{ resetLoading ? 'Сохраняем...' : 'Сохранить' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>