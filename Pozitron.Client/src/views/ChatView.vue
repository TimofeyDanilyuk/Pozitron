<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import { useAuthStore } from '../store/auth';
import { useChatStore } from '../store/chat';
import { useRouter } from 'vue-router';

const auth = useAuthStore();
const chat = useChatStore();
const router = useRouter();

const isProfileOpen = ref(false);
const isSaving = ref(false);
const messageInput = ref('');
const userPrefix = ref(auth.user?.emojiPrefix || '⚛️');
const newUsername = ref(auth.user?.username || '');
const usernameError = ref('');
const searchQuery = ref('');
const isSearchOpen = ref(false);
const messagesEnd = ref<HTMLElement | null>(null);
const isEmojiPickerOpen = ref(false);
const emojiSearch = ref('');
const isChatEmojiPickerOpen = ref(false);
const chatEmojiSearch = ref('');
const mobileView = ref<'list' | 'chat'>('list');

// ===== СТИКЕРЫ =====
const isStickerPickerOpen = ref(false);
const activeStickerPackId = ref<string | null>(null);
const isCreatePackOpen = ref(false);
const newPackName = ref('');
const creatingPack = ref(false);
const uploadingSticker = ref(false);
const stickerPackModal = ref<null | { id: string, name: string, coverUrl?: string, stickers: any[], isAdded: boolean, createdBy: string }>(null);
const stickerPackModalLoading = ref(false);

const activePack = computed(() =>
  chat.stickerPacks.find(p => p.id === activeStickerPackId.value) || chat.stickerPacks[0] || null
);

const toggleStickerPicker = async () => {
  isStickerPickerOpen.value = !isStickerPickerOpen.value;
  isChatEmojiPickerOpen.value = false;
  if (isStickerPickerOpen.value && !chat.stickerPacksLoaded) {
    await chat.loadMyPacks();
    if (chat.stickerPacks.length > 0) {
      activeStickerPackId.value = chat.stickerPacks[0]?.id ?? null;
    }
  }
};

const selectPack = (packId: string) => {
  activeStickerPackId.value = packId;
  isCreatePackOpen.value = false;
};

const sendSticker = async (stickerId: string) => {
  await chat.sendSticker(stickerId);
  isStickerPickerOpen.value = false;
};

const createPack = async () => {
  if (!newPackName.value.trim()) return;
  creatingPack.value = true;
  try {
    const pack = await chat.createPack(newPackName.value.trim());
    activeStickerPackId.value = pack.id;
    newPackName.value = '';
    isCreatePackOpen.value = false;
  } finally {
    creatingPack.value = false;
  }
};

const uploadSticker = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (!target.files || !target.files[0] || !activePack.value) return;
  uploadingSticker.value = true;
  try {
    await chat.addStickerToPack(activePack.value.id, target.files[0]);
  } finally {
    uploadingSticker.value = false;
    target.value = '';
  }
};

const onStickerClick = async (packId: string) => {
  stickerPackModalLoading.value = true;
  stickerPackModal.value = null;
  try {
    const data = await chat.getPack(packId);
    stickerPackModal.value = data;
  } finally {
    stickerPackModalLoading.value = false;
  }
};

const togglePackFromModal = async () => {
  if (!stickerPackModal.value) return;
  if (stickerPackModal.value.isAdded) {
    await chat.removePack(stickerPackModal.value.id);
    stickerPackModal.value.isAdded = false;
  } else {
    await chat.addPack(stickerPackModal.value.id);
    stickerPackModal.value.isAdded = true;
  }
};

// ===== КОНТАКТЫ ====
const isAddContactsOpen = ref(false);
const contactSearch = ref('');
const selectedUserIds = ref<Set<string>>(new Set());
const addingContacts = ref(false);

const filteredAllUsers = computed(() => {
  if (!contactSearch.value.trim()) return chat.allUsers;
  return chat.allUsers.filter(u =>
    u.username.toLowerCase().includes(contactSearch.value.toLowerCase())
  );
});

const openAddContacts = async () => {
  isAddContactsOpen.value = true;
  selectedUserIds.value = new Set();
  contactSearch.value = '';
  await chat.loadAllUsers();
};

const toggleSelectUser = (userId: string) => {
  if (selectedUserIds.value.has(userId)) {
    selectedUserIds.value.delete(userId);
  } else {
    selectedUserIds.value.add(userId);
  }
  // Триггер реактивности
  selectedUserIds.value = new Set(selectedUserIds.value);
};

const confirmAddContacts = async () => {
  if (selectedUserIds.value.size === 0) return;
  addingContacts.value = true;
  try {
    for (const userId of selectedUserIds.value) {
      await chat.addContact(userId);
    }
    isAddContactsOpen.value = false;
  } finally {
    addingContacts.value = false;
  }
};

// ===== ЭМОДЗИ =====
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

const filteredEmojis = computed(() => {
  if (!emojiSearch.value.trim()) return emojiCategories;
  const all = emojiCategories.flatMap(c => c.emojis);
  return [{ label: '🔍 Результаты', emojis: all }];
});

const filteredChatEmojis = computed(() => {
  if (!chatEmojiSearch.value.trim()) return emojiCategories;
  const all = emojiCategories.flatMap(c => c.emojis);
  return [{ label: '🔍 Результаты', emojis: all }];
});

const selectEmoji = (emoji: string) => {
  userPrefix.value = emoji;
  isEmojiPickerOpen.value = false;
  emojiSearch.value = '';
};

const insertEmoji = (emoji: string) => {
  messageInput.value += emoji;
  isChatEmojiPickerOpen.value = false;
  chatEmojiSearch.value = '';
};

const currentMessages = computed(() =>
  chat.activeChat ? (chat.messages[chat.activeChat.id] || []) : []
);

const isOnline = (userId: string) => chat.onlineUsers.includes(userId);

const scrollToBottom = () => {
  nextTick(() => messagesEnd.value?.scrollIntoView({ behavior: 'smooth' }));
};

watch(currentMessages, scrollToBottom, { deep: true });

onMounted(async () => {
  await chat.connect();
  await chat.loadChats();
  if (window.innerWidth >= 768) {
    const general = chat.chats.find(c => c.type === 0);
    if (general) await chat.openChat(general);
  }
});

onUnmounted(() => chat.disconnect());

const openChat = async (c: any) => {
  await chat.openChat(c);
  mobileView.value = 'chat';
};

const goBackToList = () => {
  mobileView.value = 'list';
};

const sendMessage = async () => {
  if (!messageInput.value.trim()) return;
  await chat.sendMessage(messageInput.value);
  messageInput.value = '';
};

const handleKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendMessage();
  }
};

const handleLogout = () => {
  chat.disconnect();
  auth.logout();
  router.push('/');
};

const onSearch = async () => {
  if (searchQuery.value.trim()) {
    await chat.searchUsers(searchQuery.value);
    isSearchOpen.value = true;
  } else {
    isSearchOpen.value = false;
  }
};

const startDm = async (userId: string) => {
  searchQuery.value = '';
  isSearchOpen.value = false;
  await chat.openDm(userId);
  mobileView.value = 'chat';
};

const openProfile = () => {
  newUsername.value = auth.user?.username || '';
  usernameError.value = '';
  userPrefix.value = auth.user?.emojiPrefix || '⚛️';
  isEmojiPickerOpen.value = false;
  isProfileOpen.value = true;
};

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

const triggerFileInput = () => document.getElementById('avatarInput')?.click();

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

const formatTime = (dateStr: string) => {
  return new Date(dateStr).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
};

const currentAvatar = computed(() => auth.user?.avatarUrl || '');
</script>

<template>
  <div class="flex h-screen bg-slate-900 text-slate-100 overflow-hidden font-sans select-none">

    <!-- ===== САЙДБАР ===== -->
    <aside :class="[
      'flex flex-col bg-slate-950/50 backdrop-blur-xl border-r border-slate-800',
      'md:relative md:w-80 md:flex',
      mobileView === 'list' ? 'absolute inset-0 z-10 flex w-full' : 'hidden md:flex'
    ]">
      <!-- Шапка -->
      <div class="p-4 border-b border-slate-800 flex justify-between items-center bg-slate-950/30 shrink-0">
        <div class="flex items-center gap-3 overflow-hidden">
          <div class="w-2.5 h-2.5 bg-green-500 rounded-full animate-pulse shrink-0 shadow-[0_0_10px_rgba(34,197,94,0.5)]"></div>
          <div class="flex items-center gap-1.5 min-w-0">
            <span class="text-lg leading-none">{{ auth.user?.emojiPrefix || '⚛️' }}</span>
            <h2 class="text-base font-black tracking-tight truncate uppercase italic text-white">
              {{ auth.user?.username || 'User' }}
            </h2>
          </div>
        </div>
        <div @click="openProfile" class="relative group cursor-pointer transition-transform active:scale-90 shrink-0">
          <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl"
               class="w-10 h-10 rounded-xl border-2 border-purple-500 object-cover">
          <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center font-bold border-2 border-white/10">
            {{ auth.user?.username?.[0]?.toUpperCase() }}
          </div>
        </div>
        <button v-if="auth.user?.role === 1" @click="router.push('/admin')"
                class="w-10 h-10 rounded-xl bg-purple-600/20 border border-purple-500/30 flex items-center justify-center hover:bg-purple-600/40 transition-all active:scale-90 shrink-0">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </button>
      </div>

      <!-- Список чатов -->
      <div class="flex-1 overflow-y-auto">
        <div v-if="chat.chats.length === 0" class="flex flex-col items-center justify-center h-full opacity-20 px-10 text-center">
          <span class="text-4xl mb-2">💬</span>
          <p class="text-xs uppercase tracking-widest font-bold">Список пуст</p>
        </div>

        <!-- Раздел КОНТАКТЫ -->
        <template v-if="chat.contactChats.length > 0">
          <div class="px-4 pt-3 pb-1 flex items-center gap-2">
            <span class="text-[10px] font-black uppercase tracking-widest text-purple-400">Контакты</span>
            <div class="flex-1 h-px bg-purple-500/20"></div>
          </div>
          <div v-for="c in chat.contactChats" :key="c.id" @click="openChat(c)"
               :class="['flex items-center gap-3 px-4 py-3 cursor-pointer transition-all active:bg-slate-700/50 hover:bg-slate-800/50',
                 chat.activeChat?.id === c.id ? 'bg-purple-600/20 border-r-2 border-purple-500' : '']">
            <div class="relative shrink-0">
              <img v-if="c.avatarUrl" :src="c.avatarUrl" class="w-12 h-12 rounded-xl object-cover">
              <div v-else class="w-12 h-12 rounded-xl bg-slate-700 flex items-center justify-center text-xl">
                {{ c.name?.[0]?.toUpperCase() }}
              </div>
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-1.5">
                <!-- Иконка контакта -->
                <svg viewBox="0 0 16 16" class="w-3 h-3 text-purple-400 shrink-0 fill-current">
                  <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm0 1a5 5 0 0 0-5 5h10a5 5 0 0 0-5-5z"/>
                </svg>
                <p class="font-bold text-sm truncate text-purple-300">{{ c.name || 'Контакт' }}</p>
              </div>
              <p class="text-xs text-slate-500 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
            </div>
            <span v-if="c.unreadCount > 0"
                  class="shrink-0 min-w-5 h-5 px-1 bg-purple-500 text-white text-xs font-bold rounded-full flex items-center justify-center">
              {{ c.unreadCount > 99 ? '99+' : c.unreadCount }}
            </span>
            <span class="text-slate-600 md:hidden flex items-center">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" class="w-5 h-5" fill="none">
                <path d="M9 6L15 12L9 18" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
            </span>
          </div>
        </template>

        <!-- Раздел ЧАТЫ -->
        <template v-if="chat.otherChats.length > 0">
          <div class="px-4 pt-3 pb-1 flex items-center gap-2">
            <span class="text-[10px] font-black uppercase tracking-widest text-slate-500">Чаты</span>
            <div class="flex-1 h-px bg-slate-700/50"></div>
          </div>
          <div v-for="c in chat.otherChats" :key="c.id" @click="openChat(c)"
               :class="['flex items-center gap-3 px-4 py-3 cursor-pointer transition-all active:bg-slate-700/50 hover:bg-slate-800/50',
                 chat.activeChat?.id === c.id ? 'bg-purple-600/20 border-r-2 border-purple-500' : '']">
            <div class="relative shrink-0">
              <img v-if="c.avatarUrl && c.type === 1" :src="c.avatarUrl" class="w-12 h-12 rounded-xl object-cover">
              <div v-else class="w-12 h-12 rounded-xl bg-slate-700 flex items-center justify-center text-xl">
                {{ c.type === 0 ? '🌐' : c.name?.[0]?.toUpperCase() }}
              </div>
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-bold text-sm truncate">{{ c.name || 'Чат' }}</p>
              <p class="text-xs text-slate-500 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
            </div>
            <span v-if="c.unreadCount > 0"
                  class="shrink-0 min-w-5 h-5 px-1 bg-purple-500 text-white text-xs font-bold rounded-full flex items-center justify-center">
              {{ c.unreadCount > 99 ? '99+' : c.unreadCount }}
            </span>
            <span class="text-slate-600 md:hidden flex items-center">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" class="w-5 h-5" fill="none">
                <path d="M9 6L15 12L9 18" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
            </span>
          </div>
        </template>
      </div>

      <!-- Футер сайдбара: кнопка контактов + поиск -->
      <div class="p-4 border-t border-slate-800 shrink-0 space-y-2">
        <!-- Кнопка добавить контакты -->
        <button @click="openAddContacts"
                class="w-full flex items-center gap-2 px-4 py-2.5 rounded-xl bg-purple-600/10 border border-purple-500/20 hover:bg-purple-600/20 transition-all active:scale-95 text-purple-400 text-sm font-bold">
          <svg viewBox="0 0 24 24" class="w-4 h-4 fill-current shrink-0">
            <path d="M19 11h-6V5a1 1 0 0 0-2 0v6H5a1 1 0 0 0 0 2h6v6a1 1 0 0 0 2 0v-6h6a1 1 0 0 0 0-2z"/>
          </svg>
          Добавить контакты...
        </button>

        <!-- Поиск пользователей для DM -->
        <div class="relative">
          <input v-model="searchQuery" @input="onSearch" type="text" placeholder="Найти пользователя..."
                 class="w-full bg-slate-900/50 border border-slate-800 rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-2 focus:ring-purple-500/50 transition-all select-text">
          <div v-if="isSearchOpen"
               class="absolute bottom-full mb-2 left-0 right-0 bg-slate-900 border border-slate-700 rounded-xl overflow-hidden shadow-2xl max-h-48 overflow-y-auto">
            <div v-for="user in chat.users" :key="user.id" @click="startDm(user.id)"
                 class="flex items-center gap-3 px-4 py-3 hover:bg-slate-800 active:bg-slate-700 cursor-pointer transition-all">
              <div :class="['w-2 h-2 rounded-full shrink-0', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-600']"></div>
              <span class="text-sm">{{ user.emojiPrefix || '' }} {{ user.username }}</span>
            </div>
            <div v-if="chat.users.length === 0" class="px-4 py-2 text-xs text-slate-500 text-center">Не найдено</div>
          </div>
        </div>
      </div>
    </aside>

    <!-- ===== ОСНОВНАЯ ОБЛАСТЬ ===== -->
    <main :class="['flex-1 flex flex-col bg-slate-900 relative', mobileView === 'list' ? 'hidden md:flex' : 'flex']">
      <div class="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://www.transparenttextures.com/patterns/carbon-fibre.png')]"></div>

      <header class="px-4 py-3 border-b border-slate-800 flex items-center justify-between bg-slate-900/80 backdrop-blur-md z-10 shrink-0">
        <div class="flex items-center gap-2">
          <button @click="goBackToList" class="md:hidden w-9 h-9 flex items-center justify-center rounded-xl hover:bg-slate-800 active:bg-slate-700 transition-all">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
              <defs><linearGradient id="g" x1="0" y1="0" x2="24" y2="24">
              <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
              <path d="M15 6L9 12L15 18" stroke="url(#g)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <div v-if="chat.activeChat" class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-slate-700 flex items-center justify-center text-base shrink-0">
              {{ chat.activeChat.type === 0 ? '🌐' : chat.activeChat.name?.[0]?.toUpperCase() }}
            </div>
            <div class="flex items-center gap-1.5">
              <p class="font-bold text-sm" :class="chat.activeChat.isContact ? 'text-purple-300' : ''">
                {{ chat.activeChat.name || 'Чат' }}
              </p>
              <!-- Иконка контакта в шапке -->
              <svg v-if="chat.activeChat.isContact" viewBox="0 0 16 16" class="w-3.5 h-3.5 text-purple-400 fill-current">
                <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm0 1a5 5 0 0 0-5 5h10a5 5 0 0 0-5-5z"/>
              </svg>
            </div>
          </div>
          <div v-else class="flex items-center gap-2">
            <div class="w-2 h-2 rounded-full bg-slate-600"></div>
            <span class="font-bold tracking-widest text-xs uppercase opacity-70">Выберите чат</span>
          </div>
        </div>
        <button @click="handleLogout"
                class="px-3 py-2 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-400 text-xs font-bold transition-all border border-red-500/20 active:scale-95">
          Выйти
        </button>
      </header>

      <!-- Лента сообщений -->
      <div class="flex-1 px-3 py-4 overflow-y-auto z-10 space-y-3 select-text">
        <div v-if="!chat.activeChat" class="flex h-full items-center justify-center opacity-20">
          <div class="text-center">
            <p class="text-4xl mb-2">💬</p>
            <p class="text-sm italic font-medium">Выберите чат</p>
          </div>
        </div>

        <template v-if="chat.activeChat">
          <div v-if="currentMessages.length === 0" class="flex justify-center pt-10 opacity-30">
            <p class="text-sm italic">Сообщений пока нет. Будь первым!</p>
          </div>

          <div v-for="msg in currentMessages" :key="msg.id"
               :class="['flex gap-2 items-end', msg.userId === auth.user?.id ? 'flex-row-reverse' : '']">
            <img v-if="msg.avatarUrl" :src="msg.avatarUrl" class="w-7 h-7 rounded-lg object-cover shrink-0 mb-5">
            <div v-else class="w-7 h-7 rounded-lg bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center text-xs font-bold shrink-0 mb-5">
              {{ msg.username?.[0]?.toUpperCase() }}
            </div>

            <div :class="['flex flex-col gap-0.5 max-w-xs sm:max-w-sm', msg.userId === auth.user?.id ? 'items-end' : 'items-start']">
              <span class="text-[10px] text-slate-500 px-1">{{ msg.emojiPrefix }} {{ msg.username }} · {{ formatTime(msg.sentAt) }}</span>

              <!-- Стикер -->
              <div v-if="msg.type === 'Sticker' && msg.attachmentUrl"
                   @click="msg.packId && onStickerClick(msg.packId)"
                   class="cursor-pointer active:scale-95 transition-transform relative">
                <img :src="msg.attachmentUrl" class="w-32 h-32 object-contain rounded-2xl" draggable="false" oncontextmenu="return false">
                <span v-if="msg.userId === auth.user?.id" class="absolute bottom-1 right-1 opacity-70">
                  <!-- Одна галочка — не прочитано -->
                  <svg v-if="!msg.isRead" viewBox="0 0 24 16" class="w-4 h-3.5 fill-white/80">
                    <path d="M2 8L7 13L14 4"/>
                    <path d="M2 8L7 13L14 4" stroke="white" stroke-width="1.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>

                  <!-- Две галочки — прочитано -->
                  <svg v-else viewBox="0 0 24 16" class="w-4 h-3.5 fill-none stroke-white stroke-[1.8px]">
                    <path d="M1 8L6 13L13 4" stroke-linecap="round" stroke-linejoin="round"/>
                    <path d="M7 8L12 13L19 4" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                </span>
              </div>

              <!-- Обычное сообщение -->
              <div v-else :class="[
                'px-3 py-2 rounded-2xl text-sm break-words leading-relaxed',
                msg.userId === auth.user?.id
                  ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white rounded-br-sm'
                  : 'bg-slate-800 text-slate-100 rounded-bl-sm'
              ]">
                {{ msg.content }}
                <span v-if="msg.userId === auth.user?.id" class="inline-flex items-center ml-2 opacity-70 translate-y-0.5">
                  <!-- Одна галочка — не прочитано -->
                  <svg v-if="!msg.isRead" viewBox="0 0 24 16" class="w-4 h-3.5 fill-white/80">
                    <path d="M2 8L7 13L14 4"/>
                    <path d="M2 8L7 13L14 4" stroke="white" stroke-width="1.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>

                  <!-- Две галочки — прочитано -->
                  <svg v-else viewBox="0 0 24 16" class="w-4 h-3.5 fill-none stroke-white stroke-[1.8px]">
                    <path d="M1 8L6 13L13 4" stroke-linecap="round" stroke-linejoin="round"/>
                    <path d="M7 8L12 13L19 4" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                </span>
              </div>
            </div>
          </div>
        </template>

        <div ref="messagesEnd"></div>
      </div>

      <!-- Футер -->
      <footer class="p-3 bg-slate-950/80 backdrop-blur-md border-t border-slate-800 z-10 shrink-0">
        <Transition name="picker">
          <div v-if="isStickerPickerOpen" class="mb-2 bg-slate-950 border border-slate-700 rounded-2xl overflow-hidden shadow-2xl" @click.stop>
            <div class="flex h-64">
              <div class="w-14 bg-slate-900 border-r border-slate-800 flex flex-col items-center py-2 gap-2 overflow-y-auto shrink-0">
                <button v-for="pack in chat.stickerPacks" :key="pack.id" @click="selectPack(pack.id)"
                        :class="['w-10 h-10 rounded-xl overflow-hidden border-2 transition-all active:scale-90',
                          activePack?.id === pack.id ? 'border-purple-500' : 'border-transparent']">
                  <img v-if="pack.coverUrl" :src="pack.coverUrl" class="w-full h-full object-cover">
                  <div v-else class="w-full h-full bg-slate-700 flex items-center justify-center text-xs font-bold">{{ pack.name[0] }}</div>
                </button>
                <button @click="isCreatePackOpen = !isCreatePackOpen; activeStickerPackId = null"
                        :class="['w-10 h-10 rounded-xl border-2 flex items-center justify-center transition-all active:scale-90',
                          isCreatePackOpen ? 'border-purple-500 bg-purple-600/20 text-purple-400' : 'border-slate-700 text-slate-500 hover:border-slate-600']">
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                  </svg>
                </button>
              </div>
              <div class="flex-1 overflow-y-auto">
                <div v-if="isCreatePackOpen" class="p-4 space-y-3">
                  <p class="text-xs font-bold text-slate-400 uppercase">Новый пак стикеров</p>
                  <input v-model="newPackName" type="text" placeholder="Название пака..."
                         class="w-full bg-slate-800 border border-slate-700 rounded-xl px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-purple-500 select-text">
                  <button @click="createPack" :disabled="creatingPack || !newPackName.trim()"
                          class="w-full bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold py-2 rounded-xl text-sm active:scale-95 transition-all disabled:opacity-50">
                    {{ creatingPack ? 'Создаём...' : 'Создать' }}
                  </button>
                </div>
                <div v-else-if="activePack">
                  <div class="flex items-center justify-between px-3 pt-3 pb-2 border-b border-slate-800">
                    <p class="text-xs font-bold text-slate-400 truncate">{{ activePack.name }}</p>
                    <label v-if="activePack.createdByMe"
                           class="cursor-pointer w-7 h-7 rounded-lg bg-purple-600/20 hover:bg-purple-600/40 border border-purple-500/30 flex items-center justify-center transition-all active:scale-90">
                      <svg v-if="!uploadingSticker" xmlns="http://www.w3.org/2000/svg" class="w-4 h-4 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                      </svg>
                      <div v-else class="w-3 h-3 border border-purple-400 border-t-transparent rounded-full animate-spin"></div>
                      <input type="file" class="hidden" accept="image/png,image/jpeg,image/gif,image/webp,video/mp4" @change="uploadSticker">
                    </label>
                  </div>
                  <div class="p-2 grid grid-cols-4 gap-1">
                    <button v-for="sticker in activePack.stickers" :key="sticker.id" @click="sendSticker(sticker.id)"
                            class="w-full aspect-square rounded-xl overflow-hidden hover:bg-slate-700 active:scale-90 transition-all p-1">
                      <img :src="sticker.url" class="w-full h-full object-contain" draggable="false" oncontextmenu="return false">
                    </button>
                    <div v-if="activePack.stickers.length === 0" class="col-span-4 flex flex-col items-center justify-center py-8 opacity-30">
                      <p class="text-xs">Нет стикеров</p>
                      <p v-if="activePack.createdByMe" class="text-xs mt-1">Нажми + чтобы добавить</p>
                    </div>
                  </div>
                </div>
                <div v-else-if="!isCreatePackOpen" class="flex flex-col items-center justify-center h-full opacity-30 p-4 text-center">
                  <p class="text-2xl mb-2">🎭</p>
                  <p class="text-xs">Нет стикеров. Нажми + чтобы создать пак</p>
                </div>
              </div>
            </div>
          </div>
        </Transition>

        <Transition name="picker">
          <div v-if="isChatEmojiPickerOpen" class="mb-2 bg-slate-950 border border-slate-700 rounded-2xl overflow-hidden shadow-2xl" @click.stop>
            <div class="p-2 border-b border-slate-800">
              <input v-model="chatEmojiSearch" type="text" placeholder="Поиск эмодзи..."
                     class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text">
            </div>
            <div class="max-h-48 overflow-y-auto p-2 space-y-3">
              <div v-for="cat in filteredChatEmojis" :key="cat.label">
                <p class="text-[10px] font-bold text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                <div class="flex flex-wrap gap-0.5">
                  <button v-for="emoji in cat.emojis" :key="emoji" @click="insertEmoji(emoji)"
                          class="w-9 h-9 flex items-center justify-center rounded-lg text-xl hover:bg-slate-700 active:scale-90 transition-all">
                    {{ emoji }}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </Transition>

        <div class="flex items-end gap-2 bg-slate-800/50 border border-slate-700/50 p-1.5 rounded-2xl focus-within:border-purple-500/50 transition-all">
          <button class="p-2 hover:bg-slate-700 rounded-xl transition-colors text-slate-400 shrink-0 self-end mb-0.5">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
              <defs><linearGradient id="ga" x1="0" y1="0" x2="24" y2="24">
              <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
              <path d="M21 11.5L12.5 20C10.3 22.2 6.7 22.2 4.5 20C2.3 17.8 2.3 14.2 4.5 12L13 3.5C14.4 2.1 16.6 2.1 18 3.5C19.4 4.9 19.4 7.1 18 8.5L9.5 17C8.8 17.7 7.7 17.7 7 17C6.3 16.3 6.3 15.2 7 14.5L14.5 7"
              stroke="url(#ga)" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <button @click.stop="toggleStickerPicker" :disabled="!chat.activeChat"
                  :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                    isStickerPickerOpen ? 'bg-purple-600/30 text-purple-300' : 'hover:bg-slate-700 text-slate-400',
                    !chat.activeChat ? 'opacity-40 cursor-not-allowed' : '']">
            <svg viewBox="0 0 24 24" class="w-5 h-5" fill="none" stroke="currentColor">
              <rect x="3" y="3" width="18" height="18" rx="4" stroke-width="2"/>
              <circle cx="8.5" cy="9" r="1.5" fill="currentColor" stroke="none"/>
              <circle cx="15.5" cy="9" r="1.5" fill="currentColor" stroke="none"/>
              <path d="M7 14.5C8.5 17 15.5 17 17 14.5" stroke-width="2" stroke-linecap="round"/>
            </svg>
          </button>
          <button @click.stop="isChatEmojiPickerOpen = !isChatEmojiPickerOpen; isStickerPickerOpen = false"
                  :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                    isChatEmojiPickerOpen ? 'bg-purple-600/30 text-purple-300' : 'hover:bg-slate-700 text-slate-400']">
            <svg viewBox="0 0 24 24" class="w-5 h-5" fill="none">
              <circle cx="12" cy="12" r="9" stroke="currentColor" stroke-width="2"/>
              <circle cx="9" cy="10" r="1" fill="currentColor"/>
              <circle cx="15" cy="10" r="1" fill="currentColor"/>
              <path d="M8 14C9 16 15 16 16 14" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
            </svg>
          </button>
          <textarea v-model="messageInput" @keydown="handleKeydown"
                    :placeholder="chat.activeChat ? 'Напиши что-нибудь...' : 'Выберите чат...'"
                    :disabled="!chat.activeChat" rows="1"
                    class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-600 select-text disabled:opacity-40 resize-none max-h-32 overflow-y-auto"
                    style="field-sizing: content;"></textarea>
          <button @click="sendMessage" :disabled="!chat.activeChat || !messageInput.trim()"
                  class="bg-gradient-to-r from-purple-600 to-indigo-600 text-white px-4 py-2.5 rounded-xl shadow-lg active:scale-95 transition-all disabled:opacity-40 disabled:cursor-not-allowed shrink-0 self-end">
            <svg viewBox="0 0 24 24" class="w-6 h-6 fill-white"><path d="M3 12L21 4L14 20L11 13L3 12Z"/></svg>
          </button>
        </div>
      </footer>
    </main>

    <!-- ===== МОДАЛКА ДОБАВЛЕНИЯ КОНТАКТОВ ===== -->
    <Transition name="fade">
      <div v-if="isAddContactsOpen" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4">
        <div @click="isAddContactsOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-slate-900 border border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <!-- Шапка -->
          <div class="p-5 border-b border-slate-800">
            <p class="font-black text-lg">Добавить контакты</p>
            <p class="text-xs text-slate-500 mt-0.5">Выбери пользователей</p>
          </div>

          <!-- Поиск -->
          <div class="px-4 pt-3">
            <input v-model="contactSearch" type="text" placeholder="Поиск..."
                   class="w-full bg-slate-800 border border-slate-700 rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-1 focus:ring-purple-500 select-text">
          </div>

          <!-- Список пользователей -->
          <div class="max-h-64 overflow-y-auto p-2 mt-2">
            <div v-if="chat.allUsers.length === 0" class="flex items-center justify-center py-8 opacity-30">
              <p class="text-sm">Загрузка...</p>
            </div>
            <div v-for="user in filteredAllUsers" :key="user.id"
                 @click="toggleSelectUser(user.id)"
                 class="flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer transition-all hover:bg-slate-800 active:bg-slate-700">
              <!-- Аватар -->
              <img v-if="user.avatarUrl" :src="user.avatarUrl" class="w-10 h-10 rounded-xl object-cover shrink-0">
              <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center text-sm font-bold shrink-0">
                {{ user.username?.[0]?.toUpperCase() }}
              </div>
              <!-- Имя -->
              <div class="flex-1 min-w-0">
                <p class="text-sm font-bold truncate">{{ user.emojiPrefix || '' }} {{ user.username }}</p>
                <div class="flex items-center gap-1 mt-0.5">
                  <div :class="['w-1.5 h-1.5 rounded-full', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-600']"></div>
                  <p class="text-xs text-slate-500">{{ isOnline(user.id) ? 'онлайн' : 'оффлайн' }}</p>
                </div>
              </div>
              <!-- Чекбокс -->
              <div :class="['w-6 h-6 rounded-lg border-2 flex items-center justify-center transition-all shrink-0',
                selectedUserIds.has(user.id)
                  ? 'bg-purple-600 border-purple-600'
                  : 'border-slate-600']">
                <svg v-if="selectedUserIds.has(user.id)" viewBox="0 0 16 16" class="w-3.5 h-3.5 fill-white">
                  <path d="M13.5 3.5L6 11L2.5 7.5L1.5 8.5L6 13L14.5 4.5L13.5 3.5Z"/>
                </svg>
              </div>
            </div>
          </div>

          <!-- Кнопки -->
          <div class="p-4 border-t border-slate-800 flex gap-3">
            <button @click="isAddContactsOpen = false"
                    class="flex-1 bg-slate-800 hover:bg-slate-700 text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">
              Отмена
            </button>
            <button @click="confirmAddContacts"
                    :disabled="selectedUserIds.size === 0 || addingContacts"
                    class="flex-1 bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl text-sm shadow-lg active:scale-95 transition-all disabled:opacity-50">
              {{ addingContacts ? 'Добавляем...' : `Добавить${selectedUserIds.size > 0 ? ` (${selectedUserIds.size})` : ''}` }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- ===== МОДАЛКА ПАКА ===== -->
    <Transition name="fade">
      <div v-if="stickerPackModal || stickerPackModalLoading" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4">
        <div @click="stickerPackModal = null; stickerPackModalLoading = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-slate-900 border border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div v-if="stickerPackModalLoading" class="flex items-center justify-center py-16">
            <div class="w-8 h-8 border-2 border-purple-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
          <div v-else-if="stickerPackModal">
            <div class="p-5 border-b border-slate-800">
              <p class="font-black text-lg">{{ stickerPackModal.name }}</p>
              <p class="text-xs text-slate-500 mt-0.5">Автор: {{ stickerPackModal.createdBy }}</p>
            </div>
            <div class="p-3 grid grid-cols-4 gap-2 max-h-56 overflow-y-auto">
              <div v-for="s in stickerPackModal.stickers" :key="s.id" class="aspect-square rounded-xl overflow-hidden bg-slate-800 p-1">
                <img :src="s.url" class="w-full h-full object-contain" draggable="false" oncontextmenu="return false">
              </div>
            </div>
            <div class="p-4 border-t border-slate-800">
              <button @click="togglePackFromModal"
                      :class="['w-full font-bold py-3 rounded-xl text-sm active:scale-95 transition-all',
                        stickerPackModal.isAdded
                          ? 'bg-red-500/10 border border-red-500/20 text-red-400 hover:bg-red-500/20'
                          : 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white shadow-lg']">
                {{ stickerPackModal.isAdded ? '— Убрать пак' : '+ Добавить пак' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Transition>

    <!-- ===== МОДАЛКА ПРОФИЛЯ ===== -->
    <Transition name="fade">
      <div v-if="isProfileOpen" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
        <div @click="isProfileOpen = false; isEmojiPickerOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-slate-900 border border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div class="h-20 bg-gradient-to-r from-purple-600 to-indigo-700"></div>
          <div class="px-6 pb-8">
            <div class="relative -mt-10 mb-4 flex justify-center">
              <div @click="triggerFileInput" class="relative group cursor-pointer">
                <img v-if="currentAvatar" :src="currentAvatar" class="w-20 h-20 rounded-2xl border-4 border-slate-900 object-cover shadow-xl">
                <div v-else class="w-20 h-20 rounded-2xl border-4 border-slate-900 bg-slate-800 flex items-center justify-center text-2xl font-bold uppercase">
                  {{ auth.user?.username?.[0] }}
                </div>
                <div class="absolute inset-0 bg-black/40 rounded-2xl opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                  <span class="text-[10px] font-bold uppercase text-white">Изменить</span>
                </div>
                <input id="avatarInput" type="file" class="hidden" accept="image/*" @change="onFileSelected">
              </div>
            </div>
            <div class="text-center mb-5">
              <h3 class="text-xl font-black">{{ auth.user?.username }}</h3>
            </div>
            <div class="space-y-4">
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Никнейм</label>
                <input v-model="newUsername" type="text" placeholder="username" maxlength="32" @input="usernameError = ''"
                       :class="['w-full bg-slate-800/50 border rounded-xl px-4 py-3 text-sm outline-none focus:ring-1 transition-all select-text',
                         usernameError ? 'border-red-500/70 focus:ring-red-500' : 'border-slate-700 focus:ring-purple-500']">
                <p v-if="usernameError" class="text-[10px] text-red-400 ml-2 mt-1">{{ usernameError }}</p>
              </div>
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Префикс (эмодзи)</label>
                <div class="flex gap-2">
                  <input v-model="userPrefix" type="text" placeholder="🚀" maxlength="2"
                         class="flex-1 bg-slate-800/50 border border-slate-700 rounded-xl px-4 py-3 text-center text-2xl outline-none focus:ring-1 focus:ring-purple-500 select-text">
                  <button @click.stop="isEmojiPickerOpen = !isEmojiPickerOpen"
                          :class="['px-4 rounded-xl border text-lg transition-all', isEmojiPickerOpen ? 'bg-purple-600/30 border-purple-500' : 'bg-slate-800/50 border-slate-700']">
                    🙂
                  </button>
                </div>
                <Transition name="picker">
                  <div v-if="isEmojiPickerOpen" class="mt-2 bg-slate-950 border border-slate-700 rounded-2xl overflow-hidden" @click.stop>
                    <div class="p-2 border-b border-slate-800">
                      <input v-model="emojiSearch" type="text" placeholder="Поиск..."
                             class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text">
                    </div>
                    <div class="max-h-44 overflow-y-auto p-2 space-y-3">
                      <div v-for="cat in filteredEmojis" :key="cat.label">
                        <p class="text-[10px] font-bold text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                        <div class="flex flex-wrap gap-0.5">
                          <button v-for="emoji in cat.emojis" :key="emoji" @click="selectEmoji(emoji)"
                                  :class="['w-9 h-9 flex items-center justify-center rounded-lg text-xl transition-all active:scale-90',
                                    userPrefix === emoji ? 'bg-purple-600/40 ring-1 ring-purple-500' : 'hover:bg-slate-700']">
                            {{ emoji }}
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </Transition>
              </div>
            </div>
            <div class="mt-6 flex gap-3">
              <button @click="isProfileOpen = false; isEmojiPickerOpen = false"
                      class="flex-1 bg-slate-800 hover:bg-slate-700 text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">
                Отмена
              </button>
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
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.picker-enter-active, .picker-leave-active { transition: all 0.15s ease; }
.picker-enter-from, .picker-leave-to { opacity: 0; transform: translateY(-6px) scale(0.98); }
</style>