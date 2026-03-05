<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import { useAuthStore } from '../store/auth';
import { useChatStore } from '../store/chat';
import { useRouter } from 'vue-router';

// Сторы и роутер
const auth = useAuthStore();
const chat = useChatStore();
const router = useRouter();

// Состояние модалки профиля
const isProfileOpen = ref(false);
const isSaving = ref(false); // идёт ли сохранение профиля

// Ввод сообщения
const messageInput = ref('');

// Поля профиля
const userPrefix = ref(auth.user?.emojiPrefix || '⚛️'); // эмодзи-префикс пользователя
const newUsername = ref(auth.user?.username || '');      // новый ник
const usernameError = ref('');                           // ошибка валидации ника

// Поиск пользователей для DM
const searchQuery = ref('');
const isSearchOpen = ref(false); // показывать ли дропдаун с результатами

// Скролл к последнему сообщению
const messagesEnd = ref<HTMLElement | null>(null);

// Пикер эмодзи для профиля
const isEmojiPickerOpen = ref(false);
const emojiSearch = ref('');

// Пикер эмодзи для поля ввода сообщения
const isChatEmojiPickerOpen = ref(false);
const chatEmojiSearch = ref('');

// Мобильная навигация — 'list' показывает список чатов, 'chat' открывает чат
const mobileView = ref<'list' | 'chat'>('list');

// Список всех категорий эмодзи
const emojiCategories = [
  { label: '😀 Смайлы', emojis: ['😀','😁','😂','🤣','😃','😄','😅','😆','😉','😊','😋','😎','😍','🥰','😘','😗','😙','😚','🙂','🤗','🤩','🤔','🤨','😐','😑','😶','🙄','😏','😣','😥','😮','🤐','😯','😪','😫','😴','😌','😛','😜','😝','🤤','😒','😓','😔','😕','🙃','🤑','😲','☹️','🙁','😖','😞','😟','😤','😢','😭','😦','😧','😨','😩','🤯','😬','😰','😱','🥵','🥶','😳','🤪','😵','🥴','😠','😡','🤬','😷','🤒','🤕','🤧','🥱'] },
  { label: '👋 Жесты', emojis: ['👋','🤚','🖐️','✋','🖖','👌','🤌','🤏','✌️','🤞','🤟','🤘','🤙','👈','👉','👆','🖕','👇','☝️','👍','👎','✊','👊','🤛','🤜','👏','🙌','👐','🤲','🤝','🙏','✍️','💅','🤳','💪','🦾'] },
  { label: '❤️ Сердца', emojis: ['❤️','🧡','💛','💚','💙','💜','🖤','🤍','🤎','💔','❣️','💕','💞','💓','💗','💖','💘','💝','💟'] },
  { label: '🐶 Животные', emojis: ['🐶','🐱','🐭','🐹','🐰','🦊','🐻','🐼','🐨','🐯','🦁','🐮','🐷','🐸','🐵','🙈','🙉','🙊','🐒','🐔','🐧','🐦','🦆','🦅','🦉','🦇','🐺','🐗','🐴','🦄','🐝','🦋','🐌','🐞','🐜','🐢','🐍','🦎','🐙','🦑','🐡','🐠','🐟','🐬','🐳','🦈','🐊','🐅','🐆','🦓','🦍','🐘','🦒','🦘','🐕','🐈','🐇','🦝','🦦','🦥','🐁','🐀','🐿️','🦔'] },
  { label: '🍕 Еда', emojis: ['🍏','🍎','🍊','🍋','🍌','🍉','🍇','🍓','🫐','🍒','🍑','🥭','🍍','🥥','🥝','🍅','🥑','🥦','🌶️','🧄','🧅','🥔','🍔','🍟','🍕','🌭','🥪','🌮','🌯','🍝','🍜','🍲','🍣','🍱','🍤','🍙','🍚','🎂','🍰','🧁','🍩','🍪','🍫','🍬','🍭','🍿','☕','🍵','🧋','🍺','🍻','🥂','🍷','🥃','🍸','🍹'] },
  { label: '⚽ Спорт', emojis: ['⚽','🏀','🏈','⚾','🎾','🏐','🏉','🎱','🏓','🏸','🥊','🥋','🎯','🏆','🥇','🥈','🥉','🏅','🎖️','🎮','🕹️','♟️','🧩'] },
  { label: '🚀 Транспорт', emojis: ['🚗','🚕','🚙','🚌','🏎️','🚓','🚑','🚒','🛻','🚚','🚜','🏍️','🛵','🚲','✈️','🚁','🚀','🛸','⛵','🚤','🛥️','🚢','🌍','🌎','🌏','🏔️','🌋','🏖️','🏝️','🏠','🏢','🏰','💒','🗼','🗽'] },
  { label: '💡 Предметы', emojis: ['⌚','📱','💻','⌨️','🖥️','📷','📸','📹','🎥','📞','☎️','📺','📻','🔋','🔌','💡','🔦','🕯️','💸','💵','💎','⚖️','🔧','🔨','⚒️','🛠️','🔩','🔮','📿','💈','⚗️','🔭','🔬','💊','💉','🧬','🧹','🚽','🚿','🛁','🧼','🧴'] },
  { label: '🎵 Музыка', emojis: ['🎵','🎶','🎼','🎤','🎧','🎷','🎸','🎹','🎺','🎻','🪕','🥁','🪘'] },
  { label: '🌟 Символы', emojis: ['✨','⭐','🌟','💫','⚡','🔥','🌈','☄️','🌊','💥','🎆','🎇','🧨','🎉','🎊','🎀','🎁','🏆','🎯','🎲','🎰','🎭','🎨','🎬','🎤','🎧'] },
];

// Фильтрация эмодзи для пикера профиля — если есть поиск, плоский список, иначе по категориям
const filteredEmojis = computed(() => {
  if (!emojiSearch.value.trim()) return emojiCategories;
  const all = emojiCategories.flatMap(c => c.emojis);
  return [{ label: '🔍 Результаты', emojis: all }];
});

// Фильтрация эмодзи для пикера в поле ввода сообщения
const filteredChatEmojis = computed(() => {
  if (!chatEmojiSearch.value.trim()) return emojiCategories;
  const all = emojiCategories.flatMap(c => c.emojis);
  return [{ label: '🔍 Результаты', emojis: all }];
});

// Выбор эмодзи для префикса профиля
const selectEmoji = (emoji: string) => {
  userPrefix.value = emoji;
  isEmojiPickerOpen.value = false;
  emojiSearch.value = '';
};

// Вставка эмодзи в конец текста сообщения
const insertEmoji = (emoji: string) => {
  messageInput.value += emoji;
  isChatEmojiPickerOpen.value = false;
  chatEmojiSearch.value = '';
};

// Сообщения активного чата из стора
const currentMessages = computed(() =>
  chat.activeChat ? (chat.messages[chat.activeChat.id] || []) : []
);

// Проверка онлайн-статуса пользователя по id
const isOnline = (userId: string) => chat.onlineUsers.includes(userId);

// Прокрутка к последнему сообщению
const scrollToBottom = () => {
  nextTick(() => messagesEnd.value?.scrollIntoView({ behavior: 'smooth' }));
};

// Автоскролл при появлении новых сообщений
watch(currentMessages, scrollToBottom, { deep: true });

// Подключение к SignalR, загрузка чатов, автооткрытие общего канала на десктопе
onMounted(async () => {
  await chat.connect();
  await chat.loadChats();
  if (window.innerWidth >= 768) {
    const general = chat.chats.find(c => c.type === 0);
    if (general) await chat.openChat(general);
  }
});

// Отключение SignalR при уходе со страницы
onUnmounted(() => chat.disconnect());

// Открытие чата — на мобилке переключает экран на 'chat'
const openChat = async (c: any) => {
  await chat.openChat(c);
  mobileView.value = 'chat';
};

// Возврат к списку чатов на мобилке
const goBackToList = () => {
  mobileView.value = 'list';
};

// Отправка сообщения и очистка поля ввода
const sendMessage = async () => {
  if (!messageInput.value.trim()) return;
  await chat.sendMessage(messageInput.value);
  messageInput.value = '';
};

// Enter без Shift — отправить, Shift+Enter — перенос строки
const handleKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendMessage();
  }
};

// Выход из аккаунта — отключение SignalR, очистка стора, редирект на авторизацию
const handleLogout = () => {
  chat.disconnect();
  auth.logout();
  router.push('/');
};

// Поиск пользователей для создания DM
const onSearch = async () => {
  if (searchQuery.value.trim()) {
    await chat.searchUsers(searchQuery.value);
    isSearchOpen.value = true;
  } else {
    isSearchOpen.value = false;
  }
};

// Открытие DM с пользователем — закрывает поиск и переходит в чат
const startDm = async (userId: string) => {
  searchQuery.value = '';
  isSearchOpen.value = false;
  await chat.openDm(userId);
  mobileView.value = 'chat';
};

// Открытие модалки профиля — сбрасывает поля к текущим значениям
const openProfile = () => {
  newUsername.value = auth.user?.username || '';
  usernameError.value = '';
  userPrefix.value = auth.user?.emojiPrefix || '⚛️';
  isEmojiPickerOpen.value = false;
  isProfileOpen.value = true;
};

// Сохранение профиля — валидация ника, обновление username и emojiPrefix
const saveProfile = async () => {
  usernameError.value = '';
  if (newUsername.value.trim().length < 3) {
    usernameError.value = 'Ник должен быть не короче 3 символов';
    return;
  }
  isSaving.value = true;
  try {
    if (newUsername.value.trim() !== auth.user?.username) {
      await auth.changeUsername(newUsername.value.trim());
    }
    await auth.updateProfile({
      emojiPrefix: userPrefix.value,
      displayName: newUsername.value.trim()
    });
    isProfileOpen.value = false;
  } catch (e: any) {
    usernameError.value = e.response?.data || 'Не удалось сохранить настройки';
  } finally {
    isSaving.value = false;
  }
};

// Клик по аватарке — открывает системный диалог выбора файла
const triggerFileInput = () => document.getElementById('avatarInput')?.click();

// Загрузка нового аватара
const onFileSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files[0]) {
    try {
      await auth.uploadAvatar(target.files[0]);
    } catch (e) {
      alert('Ошибка при загрузке фото');
    }
  }
};

// Форматирование времени сообщения в формат ЧЧ:ММ
const formatTime = (dateStr: string) => {
  return new Date(dateStr).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
};

// Текущий URL аватара пользователя
const currentAvatar = computed(() => auth.user?.avatarUrl || '');
</script>

<template>
  <!-- Корневой контейнер — flex на всю высоту экрана -->
  <div class="flex h-screen bg-slate-900 text-slate-100 overflow-hidden font-sans select-none">

    <!-- ===== САЙДБАР — список чатов ===== -->
    <!-- На мобилке занимает весь экран когда mobileView === 'list', иначе скрыт -->
    <!-- На десктопе всегда виден (md:flex) -->
    <aside :class="[
      'flex flex-col bg-slate-950/50 backdrop-blur-xl border-r border-slate-800',
      'md:relative md:w-80 md:flex',
      mobileView === 'list' ? 'absolute inset-0 z-10 flex w-full' : 'hidden md:flex'
    ]">

      <!-- Шапка сайдбара — индикатор онлайна, ник, аватарка -->
      <div class="p-4 border-b border-slate-800 flex justify-between items-center bg-slate-950/30 shrink-0">
        <div class="flex items-center gap-3 overflow-hidden">
          <!-- Зелёная точка — онлайн -->
          <div class="w-2.5 h-2.5 bg-green-500 rounded-full animate-pulse shrink-0 shadow-[0_0_10px_rgba(34,197,94,0.5)]"></div>
          <div class="flex items-center gap-1.5 min-w-0">
            <!-- Эмодзи-префикс -->
            <span class="text-lg leading-none">{{ auth.user?.emojiPrefix || '⚛️' }}</span>
            <!-- Ник текущего пользователя -->
            <h2 class="text-base font-black tracking-tight truncate uppercase italic text-white">
              {{ auth.user?.username || 'User' }}
            </h2>
          </div>
        </div>
        <!-- Аватарка — открывает модалку профиля -->
        <div @click="openProfile" class="relative group cursor-pointer transition-transform active:scale-90 shrink-0">
          <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl"
               class="w-10 h-10 rounded-xl border-2 border-purple-500 object-cover">
          <!-- Заглушка аватара с первой буквой ника -->
          <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center font-bold border-2 border-white/10">
            {{ auth.user?.username?.[0]?.toUpperCase() }}
          </div>
        </div>
      </div>

      <!-- Список чатов — скроллится если не влезает -->
      <div class="flex-1 overflow-y-auto">
        <!-- Пустое состояние -->
        <div v-if="chat.chats.length === 0" class="flex flex-col items-center justify-center h-full opacity-20 px-10 text-center">
          <span class="text-4xl mb-2">💬</span>
          <p class="text-xs uppercase tracking-widest font-bold">Список пуст</p>
        </div>

        <!-- Элемент чата — аватар, название, последнее сообщение -->
        <div v-for="c in chat.chats" :key="c.id" @click="openChat(c)"
             :class="[
               'flex items-center gap-3 px-4 py-3 cursor-pointer transition-all active:bg-slate-700/50 hover:bg-slate-800/50',
               // Подсветка активного чата
               chat.activeChat?.id === c.id ? 'bg-purple-600/20 border-r-2 border-purple-500' : ''
             ]">
          <div class="relative shrink-0">
            <!-- Аватар чата (только для DM если есть) -->
            <img v-if="c.avatarUrl && c.type === 1" :src="c.avatarUrl" class="w-12 h-12 rounded-xl object-cover">
            <!-- Иконка по умолчанию: глобус для общего канала, буква для DM -->
            <div v-else class="w-12 h-12 rounded-xl bg-slate-700 flex items-center justify-center text-xl">
              {{ c.type === 0 ? '🌐' : c.name?.[0]?.toUpperCase() }}
            </div>
          </div>
          <div class="min-w-0 flex-1">
            <!-- Название чата -->
            <p class="font-bold text-sm truncate">{{ c.name || 'Чат' }}</p>
            <!-- Превью последнего сообщения -->
            <p class="text-xs text-slate-500 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
          </div>
          <!-- Стрелка — только на мобилке, намекает что можно нажать -->
          <span class="text-slate-600 md:hidden flex items-center">
            <svg xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 24 24"
                class="w-5 h-5"
                fill="none">
                
              <path d="M9 6L15 12L9 18"
                    stroke="currentColor"
                    stroke-width="2.5"
                    stroke-linecap="round"
                    stroke-linejoin="round"/>
                    
            </svg>
          </span>
        </div>
      </div>

      <!-- Поиск пользователей для создания DM -->
      <div class="p-4 border-t border-slate-800 shrink-0">
        <div class="relative">
          <input v-model="searchQuery" @input="onSearch" type="text"
                 placeholder="Найти пользователя..."
                 class="w-full bg-slate-900/50 border border-slate-800 rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-2 focus:ring-purple-500/50 transition-all select-text">

          <!-- Дропдаун с результатами поиска -->
          <div v-if="isSearchOpen"
               class="absolute bottom-full mb-2 left-0 right-0 bg-slate-900 border border-slate-700 rounded-xl overflow-hidden shadow-2xl max-h-48 overflow-y-auto">
            <!-- Строка пользователя — индикатор онлайна + имя -->
            <div v-for="user in chat.users" :key="user.id" @click="startDm(user.id)"
                 class="flex items-center gap-3 px-4 py-3 hover:bg-slate-800 active:bg-slate-700 cursor-pointer transition-all">
              <!-- Точка онлайна/офлайна -->
              <div :class="['w-2 h-2 rounded-full shrink-0', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-600']"></div>
              <span class="text-sm">{{ user.emojiPrefix || '' }} {{ user.username }}</span>
            </div>
            <!-- Если никого не нашли -->
            <div v-if="chat.users.length === 0" class="px-4 py-2 text-xs text-slate-500 text-center">Не найдено</div>
          </div>
        </div>
      </div>
    </aside>

    <!-- ===== ОСНОВНАЯ ОБЛАСТЬ ЧАТА ===== -->
    <!-- На мобилке скрыта когда mobileView === 'list' -->
    <main :class="[
      'flex-1 flex flex-col bg-slate-900 relative',
      mobileView === 'list' ? 'hidden md:flex' : 'flex'
    ]">
      <!-- Фоновая текстура — почти прозрачная -->
      <div class="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://www.transparenttextures.com/patterns/carbon-fibre.png')]"></div>

      <!-- Шапка чата — кнопка назад (мобилка), название чата, кнопка выхода -->
      <header class="px-4 py-3 border-b border-slate-800 flex items-center justify-between bg-slate-900/80 backdrop-blur-md z-10 shrink-0">
        <div class="flex items-center gap-2">
          <!-- Кнопка назад — только на мобилке, возвращает к списку -->
          <button @click="goBackToList"
                  class="md:hidden w-9 h-9 flex items-center justify-center rounded-xl hover:bg-slate-800 active:bg-slate-700 transition-all text-purple-400 text-2xl font-bold">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
            <defs><linearGradient id="g" x1="0" y1="0" x2="24" y2="24">
            <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
            <path d="M15 6L9 12L15 18" stroke="url(#g)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
          </button>

          <!-- Активный чат — иконка и название -->
          <div v-if="chat.activeChat" class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-slate-700 flex items-center justify-center text-base shrink-0">
              {{ chat.activeChat.type === 0 ? '🌐' : chat.activeChat.name?.[0]?.toUpperCase() }}
            </div>
            <p class="font-bold text-sm">{{ chat.activeChat.name || 'Чат' }}</p>
          </div>

          <!-- Состояние когда чат не выбран -->
          <div v-else class="flex items-center gap-2">
            <div class="w-2 h-2 rounded-full bg-slate-600"></div>
            <span class="font-bold tracking-widest text-xs uppercase opacity-70">Выберите чат</span>
          </div>
        </div>

        <!-- Кнопка выхода из аккаунта -->
        <button @click="handleLogout"
                class="px-3 py-2 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-400 text-xs font-bold transition-all border border-red-500/20 active:scale-95">
          Выйти
        </button>
      </header>

      <!-- Лента сообщений — скроллится -->
      <div class="flex-1 px-3 py-4 overflow-y-auto z-10 space-y-3 select-text">

        <!-- Пустое состояние — чат не выбран -->
        <div v-if="!chat.activeChat" class="flex h-full items-center justify-center opacity-20">
          <div class="text-center">
            <p class="text-4xl mb-2">💬</p>
            <p class="text-sm italic font-medium">Выберите чат</p>
          </div>
        </div>

        <template v-if="chat.activeChat">
          <!-- Пустое состояние — сообщений ещё нет -->
          <div v-if="currentMessages.length === 0" class="flex justify-center pt-10 opacity-30">
            <p class="text-sm italic">Сообщений пока нет. Будь первым!</p>
          </div>

          <!-- Одно сообщение -->
          <div v-for="msg in currentMessages" :key="msg.id"
               :class="['flex gap-2 items-end', msg.userId === auth.user?.id ? 'flex-row-reverse' : '']">

            <!-- Аватар отправителя -->
            <img v-if="msg.avatarUrl" :src="msg.avatarUrl" class="w-7 h-7 rounded-lg object-cover shrink-0 mb-5">
            <!-- Заглушка аватара с буквой -->
            <div v-else class="w-7 h-7 rounded-lg bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center text-xs font-bold shrink-0 mb-5">
              {{ msg.username?.[0]?.toUpperCase() }}
            </div>

            <div :class="['flex flex-col gap-0.5 max-w-xs sm:max-w-sm', msg.userId === auth.user?.id ? 'items-end' : 'items-start']">
              <!-- Подпись — префикс, ник, время -->
              <span class="text-[10px] text-slate-500 px-1">{{ msg.emojiPrefix }} {{ msg.username }} · {{ formatTime(msg.sentAt) }}</span>
              <!-- Пузырь сообщения — фиолетовый для своих, серый для чужих -->
              <div :class="[
                'px-3 py-2 rounded-2xl text-sm break-words leading-relaxed',
                msg.userId === auth.user?.id
                  ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white rounded-br-sm'
                  : 'bg-slate-800 text-slate-100 rounded-bl-sm'
              ]">{{ msg.content }}</div>
            </div>
          </div>
        </template>

        <!-- Якорь для автоскролла к последнему сообщению -->
        <div ref="messagesEnd"></div>
      </div>

      <!-- Футер — поле ввода с эмодзи-пикером -->
      <footer class="p-3 bg-slate-950/80 backdrop-blur-md border-t border-slate-800 z-10 shrink-0">

        <!-- Пикер эмодзи для сообщения — появляется над полем ввода -->
        <Transition name="picker">
          <div v-if="isChatEmojiPickerOpen" class="mb-2 bg-slate-950 border border-slate-700 rounded-2xl overflow-hidden shadow-2xl" @click.stop>
            <!-- Строка поиска эмодзи -->
            <div class="p-2 border-b border-slate-800">
              <input v-model="chatEmojiSearch" type="text" placeholder="Поиск эмодзи..."
                     class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text">
            </div>
            <!-- Сетка эмодзи по категориям -->
            <div class="max-h-48 overflow-y-auto p-2 space-y-3">
              <div v-for="cat in filteredChatEmojis" :key="cat.label">
                <p class="text-[10px] font-bold text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                <div class="flex flex-wrap gap-0.5">
                  <!-- Кнопка эмодзи — вставляет в поле ввода -->
                  <button v-for="emoji in cat.emojis" :key="emoji" @click="insertEmoji(emoji)"
                          class="w-9 h-9 flex items-center justify-center rounded-lg text-xl hover:bg-slate-700 active:scale-90 transition-all">
                    {{ emoji }}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </Transition>

        <!-- Строка ввода — скрепка, кнопка эмодзи, textarea, кнопка отправки -->
        <div class="flex items-end gap-2 bg-slate-800/50 border border-slate-700/50 p-1.5 rounded-2xl focus-within:border-purple-500/50 transition-all">
          <!-- Кнопка прикрепления файла (пока не реализовано) -->
          <button class="p-2 hover:bg-slate-700 rounded-xl transition-colors text-slate-400 shrink-0 self-end mb-0.5">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
            <defs><linearGradient id="g" x1="0" y1="0" x2="24" y2="24">
            <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
            <path d="M21 11.5L12.5 20C10.3 22.2 6.7 22.2 4.5 20C2.3 17.8 2.3 14.2 4.5 12L13 3.5C14.4 2.1 16.6 2.1 18 3.5C19.4 4.9 19.4 7.1 18 8.5L9.5 17C8.8 17.7 7.7 17.7 7 17C6.3 16.3 6.3 15.2 7 14.5L14.5 7"
            stroke="url(#g)" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></svg>
          </button>

          <!-- Кнопка открытия эмодзи-пикера -->
          <button @click.stop="isChatEmojiPickerOpen = !isChatEmojiPickerOpen"
                  :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5 text-lg', isChatEmojiPickerOpen ? 'bg-purple-600/30 text-purple-300' : 'hover:bg-slate-700 text-slate-400']">
            🙂
          </button>

          <!-- Textarea — растягивается по содержимому до max-h-32, Shift+Enter = перенос строки -->
          <textarea v-model="messageInput" @keydown="handleKeydown"
                    :placeholder="chat.activeChat ? 'Напиши что-нибудь...' : 'Выберите чат...'"
                    :disabled="!chat.activeChat"
                    rows="1"
                    class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-600 select-text disabled:opacity-40 resize-none max-h-32 overflow-y-auto"
                    style="field-sizing: content;">
          </textarea>

          <!-- Кнопка отправки -->
          <button
            @click="sendMessage"
            :disabled="!chat.activeChat || !messageInput.trim()"
            class="bg-gradient-to-r from-purple-600 to-indigo-600
                  text-white px-4 py-2.5 rounded-xl
                  shadow-lg active:scale-95 transition-all
                  disabled:opacity-40 disabled:cursor-not-allowed
                  shrink-0 self-end flex items-center justify-center">

            <svg viewBox="0 0 24 24"
                class="w-6 h-6 fill-white">
              <path d="M3 12L21 4L14 20L11 13L3 12Z"/>
            </svg>

          </button>
        </div>
      </footer>
    </main>

    <!-- ===== МОДАЛКА ПРОФИЛЯ ===== -->
    <!-- На мобилке выезжает снизу (items-end), на десктопе по центру (sm:items-center) -->
    <Transition name="fade">
      <div v-if="isProfileOpen" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
        <!-- Затемнённый фон — клик закрывает модалку -->
        <div @click="isProfileOpen = false; isEmojiPickerOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>

        <!-- Карточка профиля -->
        <div class="relative bg-slate-900 border border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <!-- Шапка-градиент -->
          <div class="h-20 bg-gradient-to-r from-purple-600 to-indigo-700"></div>

          <div class="px-6 pb-8">
            <!-- Аватар — поднят вверх на баннер, клик открывает выбор файла -->
            <div class="relative -mt-10 mb-4 flex justify-center">
              <div @click="triggerFileInput" class="relative group cursor-pointer">
                <img v-if="currentAvatar" :src="currentAvatar" class="w-20 h-20 rounded-2xl border-4 border-slate-900 object-cover shadow-xl">
                <!-- Заглушка аватара -->
                <div v-else class="w-20 h-20 rounded-2xl border-4 border-slate-900 bg-slate-800 flex items-center justify-center text-2xl font-bold uppercase">
                  {{ auth.user?.username?.[0] }}
                </div>
                <!-- Оверлей "Изменить" при наведении -->
                <div class="absolute inset-0 bg-black/40 rounded-2xl opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                  <span class="text-[10px] font-bold uppercase text-white">Изменить</span>
                </div>
                <!-- Скрытый input для выбора файла -->
                <input id="avatarInput" type="file" class="hidden" accept="image/*" @change="onFileSelected">
              </div>
            </div>

            <!-- Текущий ник -->
            <div class="text-center mb-5">
              <h3 class="text-xl font-black">{{ auth.user?.username }}</h3>
            </div>

            <div class="space-y-4">
              <!-- Поле ввода нового ника -->
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Никнейм</label>
                <input v-model="newUsername" type="text" placeholder="username" maxlength="32" @input="usernameError = ''"
                       :class="['w-full bg-slate-800/50 border rounded-xl px-4 py-3 text-sm outline-none focus:ring-1 transition-all select-text', usernameError ? 'border-red-500/70 focus:ring-red-500' : 'border-slate-700 focus:ring-purple-500']">
                <!-- Ошибка валидации ника -->
                <p v-if="usernameError" class="text-[10px] text-red-400 ml-2 mt-1">{{ usernameError }}</p>
              </div>

              <!-- Поле эмодзи-префикса с пикером -->
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Префикс (эмодзи)</label>
                <div class="flex gap-2">
                  <!-- Текстовое поле для ручного ввода эмодзи -->
                  <input v-model="userPrefix" type="text" placeholder="🚀" maxlength="2"
                         class="flex-1 bg-slate-800/50 border border-slate-700 rounded-xl px-4 py-3 text-center text-2xl outline-none focus:ring-1 focus:ring-purple-500 select-text">
                  <!-- Кнопка открытия пикера -->
                  <button @click.stop="isEmojiPickerOpen = !isEmojiPickerOpen"
                          :class="['px-4 rounded-xl border text-lg transition-all', isEmojiPickerOpen ? 'bg-purple-600/30 border-purple-500' : 'bg-slate-800/50 border-slate-700']">
                    🙂
                  </button>
                </div>

                <!-- Пикер эмодзи профиля -->
                <Transition name="picker">
                  <div v-if="isEmojiPickerOpen" class="mt-2 bg-slate-950 border border-slate-700 rounded-2xl overflow-hidden" @click.stop>
                    <!-- Поиск -->
                    <div class="p-2 border-b border-slate-800">
                      <input v-model="emojiSearch" type="text" placeholder="Поиск..."
                             class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text">
                    </div>
                    <!-- Сетка эмодзи -->
                    <div class="max-h-44 overflow-y-auto p-2 space-y-3">
                      <div v-for="cat in filteredEmojis" :key="cat.label">
                        <p class="text-[10px] font-bold text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                        <div class="flex flex-wrap gap-0.5">
                          <!-- Активный эмодзи подсвечивается фиолетовым -->
                          <button v-for="emoji in cat.emojis" :key="emoji" @click="selectEmoji(emoji)"
                                  :class="['w-9 h-9 flex items-center justify-center rounded-lg text-xl transition-all active:scale-90', userPrefix === emoji ? 'bg-purple-600/40 ring-1 ring-purple-500' : 'hover:bg-slate-700']">
                            {{ emoji }}
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </Transition>
              </div>
            </div>

            <!-- Кнопки действий -->
            <div class="mt-6 flex gap-3">
              <!-- Отмена — закрывает без сохранения -->
              <button @click="isProfileOpen = false; isEmojiPickerOpen = false"
                      class="flex-1 bg-slate-800 hover:bg-slate-700 text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">
                Отмена
              </button>
              <!-- Сохранить — вызывает saveProfile -->
              <button @click="saveProfile" :disabled="isSaving"
                      class="flex-1 bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl text-sm shadow-lg active:scale-95 transition-all disabled:opacity-50">
                {{ isSaving ? 'Ждем...' : 'Сохранить' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Transition>

  </div>
</template>

<style scoped>
/* Анимация появления модалки профиля */
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* Анимация выезда эмодзи-пикера */
.picker-enter-active, .picker-leave-active { transition: all 0.15s ease; }
.picker-enter-from, .picker-leave-to { opacity: 0; transform: translateY(-6px) scale(0.98); }
</style>