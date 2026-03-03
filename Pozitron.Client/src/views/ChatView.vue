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
const messagesEnd = ref<HTMLElement | null>(null);
const isSearchOpen = ref(false);
const isEmojiPickerOpen = ref(false);
const emojiSearch = ref('');

// Категории эмодзи
const emojiCategories = [
  {
    label: '😀 Смайлы',
    emojis: ['😀','😁','😂','🤣','😃','😄','😅','😆','😉','😊','😋','😎','😍','🥰','😘','😗','😙','😚','🙂','🤗','🤩','🤔','🤨','😐','😑','😶','🙄','😏','😣','😥','😮','🤐','😯','😪','😫','😴','😌','😛','😜','😝','🤤','😒','😓','😔','😕','🙃','🤑','😲','☹️','🙁','😖','😞','😟','😤','😢','😭','😦','😧','😨','😩','🤯','😬','😰','😱','🥵','🥶','😳','🤪','😵','🥴','😠','😡','🤬','😷','🤒','🤕','🤧','🥱']
  },
  {
    label: '👋 Жесты',
    emojis: ['👋','🤚','🖐️','✋','🖖','👌','🤌','🤏','✌️','🤞','🤟','🤘','🤙','👈','👉','👆','🖕','👇','☝️','👍','👎','✊','👊','🤛','🤜','👏','🙌','👐','🤲','🤝','🙏','✍️','💅','🤳','💪','🦾','🦿','🦵','🦶','👂','🦻','👃','🫀','🫁','🧠','🦷','🦴','👀','👁️','👅','👄']
  },
  {
    label: '❤️ Сердца',
    emojis: ['❤️','🧡','💛','💚','💙','💜','🖤','🤍','🤎','💔','❣️','💕','💞','💓','💗','💖','💘','💝','💟','☮️','✝️','☯️','🕉️','🔯','♈','♉','♊','♋','♌','♍','♎','♏','♐','♑','♒','♓','⛎']
  },
  {
    label: '🐶 Животные',
    emojis: ['🐶','🐱','🐭','🐹','🐰','🦊','🐻','🐼','🐨','🐯','🦁','🐮','🐷','🐽','🐸','🐵','🙈','🙉','🙊','🐒','🐔','🐧','🐦','🐤','🦆','🦅','🦉','🦇','🐺','🐗','🐴','🦄','🐝','🐛','🦋','🐌','🐞','🐜','🦟','🦗','🕷️','🦂','🐢','🐍','🦎','🦖','🦕','🐙','🦑','🦐','🦞','🦀','🐡','🐠','🐟','🐬','🐳','🐋','🦈','🐊','🐅','🐆','🦓','🦍','🦧','🐘','🦛','🦏','🐪','🐫','🦒','🦘','🐃','🐂','🐄','🐎','🐖','🐏','🐑','🦙','🐐','🦌','🐕','🐩','🦮','🐕‍🦺','🐈','🐈‍⬛','🐓','🦃','🦚','🦜','🦢','🦩','🕊️','🐇','🦝','🦨','🦡','🦫','🦦','🦥','🐁','🐀','🐿️','🦔']
  },
  {
    label: '🍕 Еда',
    emojis: ['🍏','🍎','🍐','🍊','🍋','🍌','🍉','🍇','🍓','🫐','🍈','🍒','🍑','🥭','🍍','🥥','🥝','🍅','🍆','🥑','🥦','🥬','🥒','🌶️','🫑','🧄','🧅','🥔','🍠','🥐','🥯','🍞','🥖','🥨','🧀','🥚','🍳','🧈','🥞','🧇','🥓','🥩','🍗','🍖','🦴','🌭','🍔','🍟','🍕','🫓','🥪','🥙','🧆','🌮','🌯','🫔','🥗','🥘','🫕','🥫','🍝','🍜','🍲','🍛','🍣','🍱','🥟','🦪','🍤','🍙','🍚','🍘','🍥','🥮','🍢','🧁','🍰','🎂','🍮','🍭','🍬','🍫','🍿','🍩','🍪','🌰','🥜','🍯','🧃','🥤','🧋','☕','🍵','🫖','🍺','🍻','🥂','🍷','🫗','🥃','🍸','🍹','🧉','🍾']
  },
  {
    label: '⚽ Спорт',
    emojis: ['⚽','🏀','🏈','⚾','🥎','🎾','🏐','🏉','🥏','🎱','🪀','🏓','🏸','🏒','🏑','🥍','🏏','🪃','🥅','⛳','🪁','🎣','🤿','🎽','🎿','🛷','🥌','🪂','🏋️','🤸','🤼','🤺','🏇','⛷️','🏂','🪆','🥊','🥋','🎯','🤾','🏌️','🏄','🚣','🧘','🧗','🚵','🚴','🏆','🥇','🥈','🥉','🏅','🎖️','🏵️','🎗️','🎟️','🎫']
  },
  {
    label: '🚀 Транспорт',
    emojis: ['🚗','🚕','🚙','🚌','🚎','🏎️','🚓','🚑','🚒','🚐','🛻','🚚','🚛','🚜','🏍️','🛵','🛺','🚲','🛴','🛹','🛼','🚏','🛣️','🛤️','⛽','🚧','⚓','🛟','⛵','🚤','🛥️','🛳️','⛴️','🚢','✈️','🛩️','🛫','🛬','🪂','💺','🚁','🚟','🚠','🚡','🛰️','🚀','🛸','🪐','🌍','🌎','🌏','🌐','🗺️','🧭','🌋','⛰️','🏔️','🗻','🏕️','🏖️','🏜️','🏝️','🏞️','🏟️','🏛️','🏗️','🧱','🏘️','🏚️','🏠','🏡','🏢','🏣','🏤','🏥','🏦','🏨','🏩','🏪','🏫','🏬','🏭','🏯','🏰','💒','🗼','🗽','⛪','🕌','🛕','🕍','⛩️','🕋']
  },
  {
    label: '💡 Предметы',
    emojis: ['⌚','📱','📲','💻','⌨️','🖥️','🖨️','🖱️','🖲️','🕹️','🗜️','💽','💾','💿','📀','📼','📷','📸','📹','🎥','📽️','🎞️','📞','☎️','📟','📠','📺','📻','🧭','⏱️','⏲️','⏰','🕰️','⌛','⏳','📡','🔋','🔌','💡','🔦','🕯️','🪔','🧯','🛢️','💸','💵','💴','💶','💷','💰','💳','💎','⚖️','🪙','🧰','🪛','🔧','🔨','⚒️','🛠️','⛏️','🪚','🔩','🪤','🧲','🔫','💣','🪓','🔪','🗡️','⚔️','🛡️','🚬','🪦','⚰️','⚱️','🏺','🔮','📿','🧿','💈','⚗️','🔭','🔬','🩺','🩻','🩹','🩼','💊','💉','🩸','🧬','🦠','🧫','🧪','🌡️','🧹','🪣','🧺','🧻','🚽','🚿','🛁','🪥','🧼','🫧','🪒','🧴','🧷','🧽','🧹','🧺','🪣','🧻']
  },
  {
    label: '🎵 Музыка',
    emojis: ['🎵','🎶','🎼','🎤','🎧','🎷','🎸','🎹','🎺','🎻','🪕','🥁','🪘','🎙️','📯','🎚️','🎛️']
  },
  {
    label: '🌟 Символы',
    emojis: ['✨','⭐','🌟','💫','⚡','🔥','🌈','☄️','🌊','💥','🎆','🎇','🧨','🎉','🎊','🎋','🎍','🎎','🎏','🎐','🎑','🧧','🎀','🎁','🎗️','🎟️','🎫','🏆','🥇','🥈','🥉','🥊','🥋','🎯','🎲','🎰','🎳','🎮','🕹️','♟️','🧩','🪆','🪅','🎭','🎨','🖼️','🎪','🤹','🎬','🎤','🎧','🎼','🎵','🎶','🎹','🥁','🪘','🎷','🎸','🎺','🎻','🪕']
  },
];

const filteredEmojis = computed(() => {
  if (!emojiSearch.value.trim()) return emojiCategories;
  const all = emojiCategories.flatMap(c => c.emojis);
  return [{ label: '🔍 Результаты', emojis: all }];
});

const selectEmoji = (emoji: string) => {
  userPrefix.value = emoji;
  isEmojiPickerOpen.value = false;
  emojiSearch.value = '';
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
  const general = chat.chats.find(c => c.type === 0);
  if (general) await chat.openChat(general);
});

onUnmounted(() => chat.disconnect());

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
      console.error(e);
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

    <!-- Сайдбар -->
    <aside class="w-80 border-r border-slate-800 flex flex-col bg-slate-950/50 backdrop-blur-xl">

      <!-- Шапка профиля -->
      <div class="p-5 border-b border-slate-800 flex justify-between items-center bg-slate-950/30">
        <div class="flex items-center gap-3 overflow-hidden">
          <div class="w-2.5 h-2.5 bg-green-500 rounded-full animate-pulse shrink-0 shadow-[0_0_10px_rgba(34,197,94,0.5)]"></div>
          <div class="flex items-center gap-1.5 min-w-0">
            <span class="text-lg leading-none">{{ auth.user?.emojiPrefix || '⚛️' }}</span>
            <h2 class="text-lg font-black tracking-tight truncate uppercase italic text-white drop-shadow-sm">
              {{ auth.user?.username || 'User' }}
            </h2>
          </div>
        </div>

        <div @click="openProfile" class="relative group cursor-pointer transition-transform active:scale-90 shrink-0">
          <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl"
               class="w-10 h-10 rounded-xl border-2 border-purple-500 shadow-lg shadow-purple-500/20 object-cover">
          <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center font-bold border-2 border-white/10 group-hover:border-purple-400 transition-colors">
            {{ auth.user?.username?.[0]?.toUpperCase() }}
          </div>
        </div>
      </div>

      <!-- Список чатов -->
      <div class="flex-1 overflow-y-auto">
        <div v-if="chat.chats.length === 0" class="flex flex-col items-center justify-center h-full opacity-20 px-10 text-center">
          <span class="text-4xl mb-2">💬</span>
          <p class="text-xs uppercase tracking-widest font-bold">Список пуст</p>
        </div>

        <div
          v-for="c in chat.chats"
          :key="c.id"
          @click="chat.openChat(c)"
          :class="[
            'flex items-center gap-3 px-4 py-3 cursor-pointer transition-all hover:bg-slate-800/50',
            chat.activeChat?.id === c.id ? 'bg-purple-600/20 border-r-2 border-purple-500' : ''
          ]"
        >
          <div class="relative shrink-0">
            <img v-if="c.avatarUrl && c.type === 1" :src="c.avatarUrl" class="w-10 h-10 rounded-xl object-cover">
            <div v-else class="w-10 h-10 rounded-xl bg-slate-700 flex items-center justify-center text-lg">
              {{ c.type === 0 ? '🌐' : c.name?.[0]?.toUpperCase() }}
            </div>
          </div>
          <div class="min-w-0 flex-1">
            <p class="font-bold text-sm truncate">{{ c.name || 'Чат' }}</p>
            <p class="text-xs text-slate-500 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
          </div>
        </div>
      </div>

      <!-- Поиск пользователей для DM -->
      <div class="p-4 border-t border-slate-800">
        <div class="relative">
          <input
            v-model="searchQuery"
            @input="onSearch"
            type="text"
            placeholder="Найти пользователя для DM..."
            class="w-full bg-slate-900/50 border border-slate-800 rounded-xl px-4 py-2 text-sm outline-none focus:ring-2 focus:ring-purple-500/50 transition-all select-text"
          >
          <div
            v-if="isSearchOpen"
            class="absolute bottom-full mb-2 left-0 right-0 bg-slate-900 border border-slate-700 rounded-xl overflow-hidden shadow-2xl max-h-48 overflow-y-auto"
          >
            <div
              v-for="user in chat.users"
              :key="user.id"
              @click="startDm(user.id)"
              class="flex items-center gap-3 px-4 py-2.5 hover:bg-slate-800 cursor-pointer transition-all"
            >
              <div :class="['w-2 h-2 rounded-full shrink-0', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-600']"></div>
              <span class="text-sm">{{ user.emojiPrefix || '' }} {{ user.username }}</span>
            </div>
            <div v-if="chat.users.length === 0" class="px-4 py-2 text-xs text-slate-500 text-center">
              Пользователи не найдены
            </div>
          </div>
        </div>
      </div>
    </aside>

    <!-- Основная область -->
    <main class="flex-1 flex flex-col bg-slate-900 relative">
      <div class="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://www.transparenttextures.com/patterns/carbon-fibre.png')]"></div>

      <!-- Шапка чата -->
      <header class="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-900/80 backdrop-blur-md z-10">
        <div class="flex items-center gap-3">
          <div :class="['w-2 h-2 rounded-full', chat.activeChat ? 'bg-green-500' : 'bg-slate-600']"></div>
          <span class="font-bold tracking-widest text-xs uppercase opacity-70">
            {{ chat.activeChat?.name || 'Выберите чат' }}
          </span>
        </div>
        <div class="flex items-center gap-4">
          <button
            @click="handleLogout"
            class="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-400 text-xs font-bold transition-all border border-red-500/20 active:scale-95 cursor-pointer"
          >
            <span>Выйти</span>
          </button>
        </div>
      </header>

      <!-- Сообщения -->
      <div class="flex-1 p-6 overflow-y-auto z-10 space-y-4 select-text">
        <div v-if="!chat.activeChat" class="flex h-full items-center justify-center opacity-20">
          <div class="text-center">
            <p class="text-4xl mb-2">💬</p>
            <p class="text-sm italic font-medium">Выберите чат, чтобы начать общение</p>
          </div>
        </div>

        <template v-if="chat.activeChat">
          <div v-if="currentMessages.length === 0" class="flex justify-center pt-10 opacity-30">
            <p class="text-sm italic">Сообщений пока нет. Будь первым!</p>
          </div>

          <div
            v-for="msg in currentMessages"
            :key="msg.id"
            :class="['flex gap-3 items-end', msg.userId === auth.user?.id ? 'flex-row-reverse' : '']"
          >
            <img v-if="msg.avatarUrl" :src="msg.avatarUrl" class="w-8 h-8 rounded-lg object-cover shrink-0 mb-5">
            <div v-else class="w-8 h-8 rounded-lg bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center text-xs font-bold shrink-0 mb-5">
              {{ msg.username?.[0]?.toUpperCase() }}
            </div>

            <div :class="['flex flex-col gap-1 max-w-xs lg:max-w-md', msg.userId === auth.user?.id ? 'items-end' : 'items-start']">
              <span class="text-[10px] text-slate-500 px-1">
                {{ msg.emojiPrefix }} {{ msg.username }} · {{ formatTime(msg.sentAt) }}
              </span>
              <div :class="[
                'px-4 py-2 rounded-2xl text-sm break-words',
                msg.userId === auth.user?.id
                  ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white rounded-br-sm'
                  : 'bg-slate-800 text-slate-100 rounded-bl-sm'
              ]">
                {{ msg.content }}
              </div>
            </div>
          </div>
        </template>

        <div ref="messagesEnd"></div>
      </div>

      <!-- Ввод сообщения -->
      <footer class="p-6 bg-gradient-to-t from-slate-950 to-transparent z-10">
        <div class="max-w-4xl mx-auto flex items-center gap-3 bg-slate-800/50 border border-slate-700/50 p-2 rounded-2xl backdrop-blur-md shadow-2xl focus-within:border-purple-500/50 transition-all">
          <button class="p-2.5 hover:bg-slate-700 rounded-xl transition-colors text-slate-400">📎</button>
          <input
            v-model="messageInput"
            @keydown="handleKeydown"
            type="text"
            :placeholder="chat.activeChat ? 'Напиши что-нибудь...' : 'Выберите чат...'"
            :disabled="!chat.activeChat"
            class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-600 select-text disabled:opacity-40"
          >
          <button
            @click="sendMessage"
            :disabled="!chat.activeChat || !messageInput.trim()"
            class="bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white px-6 py-2.5 rounded-xl font-black shadow-lg shadow-purple-600/20 active:scale-95 transition-all text-xs uppercase tracking-tighter disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Отправить
          </button>
        </div>
      </footer>
    </main>

    <!-- Модалка профиля -->
    <Transition name="fade">
      <div v-if="isProfileOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div @click="isProfileOpen = false; isEmojiPickerOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>

        <div class="relative bg-slate-900 border border-white/10 w-full max-w-sm rounded-3xl overflow-hidden shadow-2xl">
          <div class="h-20 bg-gradient-to-r from-purple-600 to-indigo-700"></div>

          <div class="px-8 pb-8">
            <!-- Аватар -->
            <div class="relative -mt-10 mb-4 flex justify-center">
              <div @click="triggerFileInput" class="relative group cursor-pointer">
                <img v-if="currentAvatar" :src="currentAvatar"
                     class="w-20 h-20 rounded-2xl border-4 border-slate-900 object-cover shadow-xl">
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

              <!-- Ник -->
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Никнейм</label>
                <input
                  v-model="newUsername"
                  type="text"
                  placeholder="username"
                  maxlength="32"
                  @input="usernameError = ''"
                  :class="[
                    'w-full bg-slate-800/50 border rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-1 transition-all select-text',
                    usernameError ? 'border-red-500/70 focus:ring-red-500' : 'border-slate-700 focus:ring-purple-500'
                  ]"
                >
                <p v-if="usernameError" class="text-[10px] text-red-400 ml-2 mt-1">{{ usernameError }}</p>
              </div>

              <!-- Префикс + кнопка пикера -->
              <div>
                <label class="text-[10px] font-bold text-slate-500 uppercase ml-2 mb-1 block">Префикс (эмодзи)</label>
                <div class="flex gap-2">
                  <!-- Инпут префикса -->
                  <input
                    v-model="userPrefix"
                    type="text"
                    placeholder="🚀"
                    maxlength="2"
                    class="flex-1 bg-slate-800/50 border border-slate-700 rounded-xl px-4 py-2.5 text-center text-2xl outline-none focus:ring-1 focus:ring-purple-500 transition-all select-text"
                  >
                  <!-- Кнопка открытия пикера -->
                  <button
                    @click.stop="isEmojiPickerOpen = !isEmojiPickerOpen"
                    :class="[
                      'px-3 rounded-xl border transition-all text-lg',
                      isEmojiPickerOpen
                        ? 'bg-purple-600/30 border-purple-500 text-purple-300'
                        : 'bg-slate-800/50 border-slate-700 hover:border-slate-500 text-slate-300'
                    ]"
                    title="Выбрать эмодзи"
                  >
                    🙂
                  </button>
                </div>

                <!-- Пикер эмодзи -->
                <Transition name="picker">
                  <div
                    v-if="isEmojiPickerOpen"
                    class="mt-2 bg-slate-950 border border-slate-700 rounded-2xl overflow-hidden shadow-2xl"
                    @click.stop
                  >
                    <!-- Поиск -->
                    <div class="p-2 border-b border-slate-800">
                      <input
                        v-model="emojiSearch"
                        type="text"
                        placeholder="Поиск..."
                        class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs outline-none focus:ring-1 focus:ring-purple-500 transition-all select-text"
                      >
                    </div>

                    <!-- Список эмодзи по категориям -->
                    <div class="max-h-52 overflow-y-auto p-2 space-y-3">
                      <div v-for="cat in filteredEmojis" :key="cat.label">
                        <p class="text-[10px] font-bold text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                        <div class="flex flex-wrap gap-0.5">
                          <button
                            v-for="emoji in cat.emojis"
                            :key="emoji"
                            @click="selectEmoji(emoji)"
                            :class="[
                              'w-8 h-8 flex items-center justify-center rounded-lg text-lg transition-all hover:bg-slate-700 active:scale-90',
                              userPrefix === emoji ? 'bg-purple-600/40 ring-1 ring-purple-500' : ''
                            ]"
                          >
                            {{ emoji }}
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </Transition>
              </div>
            </div>

            <!-- Кнопки -->
            <div class="mt-6 flex gap-3">
              <button
                @click="isProfileOpen = false; isEmojiPickerOpen = false"
                class="flex-1 bg-slate-800 hover:bg-slate-700 text-white font-bold py-3 rounded-xl text-xs transition-all cursor-pointer"
              >
                Отмена
              </button>
              <button
                @click="saveProfile"
                :disabled="isSaving"
                class="flex-1 bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl text-xs shadow-lg active:scale-95 transition-all cursor-pointer disabled:opacity-50"
              >
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
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.picker-enter-active,
.picker-leave-active {
  transition: all 0.15s ease;
}
.picker-enter-from,
.picker-leave-to {
  opacity: 0;
  transform: translateY(-6px) scale(0.98);
}
</style>