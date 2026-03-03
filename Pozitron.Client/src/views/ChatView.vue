<script setup lang="ts">
import { ref, computed } from 'vue';
import { useAuthStore } from '../store/auth.ts'; 
import { useRouter } from 'vue-router';

const auth = useAuthStore();
const router = useRouter();

// Состояния модалок
const isProfileOpen = ref(false);
const isSaving = ref(false);

// Реактивные данные
const chats = ref([]);
const userPrefix = ref(auth.user?.emojiPrefix || '⚛️');

const currentAvatar = computed(() => auth.user?.avatarUrl || '');

// Метод выхода
const handleLogout = () => {
  auth.logout();
  router.push('/');
};

// Вызов окна выбора файла
const triggerFileInput = () => {
  document.getElementById('avatarInput')?.click();
};

// Обработка аватарки
const onFileSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files[0]) {
    const file = target.files[0];
    try {
      await auth.uploadAvatar(file);
    } catch (e) {
      console.error(e);
      alert("Ошибка при загрузке фото");
    }
  }
};

// Сохранение профиля
const saveProfile = async () => {
  isSaving.value = true;
  try {
    await auth.updateProfile({ 
      emojiPrefix: userPrefix.value,
      displayName: auth.user?.username
    });
    isProfileOpen.value = false;
  } catch (e) {
    console.error(e);
    alert("Не удалось сохранить настройки");
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <div class="flex h-screen bg-slate-900 text-slate-100 overflow-hidden font-sans select-none">
    
    <aside class="w-80 border-r border-slate-800 flex flex-col bg-slate-950/50 backdrop-blur-xl">
      <div class="p-5 border-b border-slate-800 flex justify-between items-center bg-slate-950/30">
        <div class="flex items-center gap-3 overflow-hidden">
          <div class="w-2.5 h-2.5 bg-green-500 rounded-full animate-pulse shrink-0 shadow-[0_0_10px_rgba(34,197,94,0.5)]"></div>
          <div class="flex items-center gap-1.5 min-w-0">
            <span class="text-lg leading-none">{{ userPrefix }}</span>
            <h2 class="text-lg font-black tracking-tight truncate uppercase italic text-white drop-shadow-sm">
              {{ auth.user?.username || 'User' }}
            </h2>
          </div>
        </div>
        
        <div @click="isProfileOpen = true" class="relative group cursor-pointer transition-transform active:scale-90 shrink-0">
          <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl" 
               class="w-10 h-10 rounded-xl border-2 border-purple-500 shadow-lg shadow-purple-500/20 object-cover">
          <div v-else class="w-10 h-10 rounded-xl bg-linear-to-tr from-purple-600 to-indigo-600 flex items-center justify-center font-bold border-2 border-white/10 group-hover:border-purple-400 transition-colors">
            {{ auth.user?.username?.[0].toUpperCase() }}
          </div>
        </div>
      </div>
      
      <div class="p-4">
        <input type="text" placeholder="Поиск контактов..." 
               class="w-full bg-slate-900/50 border border-slate-800 rounded-xl px-4 py-2 text-sm outline-none focus:ring-2 focus:ring-purple-500/50 transition-all select-text">
      </div>

      <div class="flex-1 overflow-y-auto custom-scrollbar">
        <div v-if="chats.length === 0" class="flex flex-col items-center justify-center h-full opacity-20 px-10 text-center">
          <span class="text-4xl mb-2">💬</span>
          <p class="text-xs uppercase tracking-widest font-bold">Список пуст</p>
        </div>
      </div>
    </aside>

    <main class="flex-1 flex flex-col bg-slate-900 relative">
      <div class="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://www.transparenttextures.com/patterns/carbon-fibre.png')]"></div>

      <header class="h-18.25 px-6 border-b border-slate-800 flex items-center justify-between bg-slate-900/80 backdrop-blur-md z-10">
        <div class="flex items-center gap-3">
            <div class="w-2 h-2 bg-green-500 rounded-full"></div>
            <span class="font-bold tracking-widest text-xs uppercase opacity-70">Общий канал</span>
        </div>
        
        <div class="flex items-center gap-4">
            <button @click="handleLogout" class="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-400 text-xs font-bold transition-all border border-red-500/20 active:scale-95 cursor-pointer">
               <span>Выйти</span>
            </button>
        </div>
      </header>

      <div class="flex-1 p-6 overflow-y-auto z-10 custom-scrollbar flex flex-col justify-center items-center opacity-20 select-text">
         <p class="text-sm font-medium italic select-none">Выберите чат, чтобы начать общение</p>
      </div>

      <footer class="p-6 bg-linear-to-t from-slate-950 to-transparent z-10">
        <div class="max-w-4xl mx-auto flex items-center gap-3 bg-slate-800/50 border border-slate-700/50 p-2 rounded-2xl backdrop-blur-md shadow-2xl focus-within:border-purple-500/50 transition-all">
          <button class="p-2.5 hover:bg-slate-700 rounded-xl transition-colors text-slate-400">📎</button>
          <input type="text" placeholder="Напиши что-нибудь..." 
            class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-600 select-text">
          <button class="bg-linear-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white px-6 py-2.5 rounded-xl font-black shadow-lg shadow-purple-600/20 active:scale-95 transition-all text-xs uppercase tracking-tighter">
            Отправить
          </button>
        </div>
      </footer>
    </main>

    <Transition name="fade">
      <div v-if="isProfileOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div @click="isProfileOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        
        <div class="relative bg-slate-900 border border-white/10 w-full max-w-sm rounded-4xl overflow-hidden shadow-2xl">
          <div class="h-20 bg-linear-to-r from-purple-600 to-indigo-700"></div>
          
          <div class="px-8 pb-8">
            <div class="relative -mt-10 mb-4 flex justify-center">
              <div @click="triggerFileInput" class="relative group cursor-pointer">
                <img v-if="currentAvatar" :src="currentAvatar" class="w-20 h-20 rounded-2xl border-4 border-slate-900 object-cover shadow-xl">
                <div v-else class="w-20 h-20 rounded-2xl border-4 border-slate-900 bg-slate-800 flex items-center justify-center text-2xl font-bold uppercase">
                  {{ auth.user?.username?.[0] }}
                </div>
                <div class="absolute inset-0 bg-black/40 rounded-2xl opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity border-2 border-white/20">
                   <span class="text-[10px] font-bold uppercase tracking-tighter text-white">Изменить</span>
                </div>
                <input id="avatarInput" type="file" class="hidden" accept="image/*" @change="onFileSelected">
              </div>
            </div>

            <div class="text-center mb-6">
              <h3 class="text-xl font-black tracking-tight">{{ auth.user?.username }}</h3>
            </div>

            <div class="space-y-4">
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Префикс (только эмодзи)</label>
                <input v-model="userPrefix" type="text" placeholder="🚀" maxlength="2"
                       class="w-full bg-slate-800/50 border border-slate-700 rounded-xl px-4 py-2.5 text-center text-2xl outline-none focus:ring-1 focus:ring-purple-500 transition-all select-text">
              </div>
            </div>

            <div class="mt-8 flex gap-3">
              <button @click="isProfileOpen = false" class="flex-1 bg-slate-800 hover:bg-slate-700 text-white font-bold py-3 rounded-xl text-xs transition-all cursor-pointer">
                Отмена
              </button>
              <button @click="saveProfile" :disabled="isSaving" class="flex-1 bg-linear-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl text-xs shadow-lg active:scale-95 transition-all cursor-pointer disabled:opacity-50">
                {{ isSaving ? 'Ждем...' : 'Сохранить' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>