<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import { useAuthStore } from '../store/auth';
import { useChatStore } from '../store/chat';
import { useRouter } from 'vue-router';
import api from '../api';

const auth = useAuthStore();
const chat = useChatStore();
const router = useRouter();

// ===== BLOB КЕШ ДЛЯ ЗАЩИЩЁННЫХ ФАЙЛОВ =====
const blobCache = new Map<string, string>();

const fetchFileAsBlob = async (url: string): Promise<string> => {
  if (blobCache.has(url)) return blobCache.get(url)!;
  try {
    const response = await api.get(url, { responseType: 'blob' });
    const blobUrl = URL.createObjectURL(response.data);
    blobCache.set(url, blobUrl);
    return blobUrl;
  } catch {
    return url; // fallback
  }
};

// Реактивный кеш для шаблона
const resolvedUrls = ref<Record<string, string>>({});

const resolveUrl = async (url: string) => {
  if (!url || resolvedUrls.value[url]) return;
  if (!url.includes('/api/files/')) {
    resolvedUrls.value[url] = url;
    return;
  }
  resolvedUrls.value[url] = await fetchFileAsBlob(url);
};

const getResolvedUrl = (url: string | undefined): string => {
  if (!url) return '';
  if (!url.includes('/api/files/')) return url;
  return resolvedUrls.value[url] || '';
};

const isProfileOpen = ref(false);
const isSaving = ref(false);
const messageInput = ref('');
const userPrefix = ref(auth.user?.emojiPrefix || '⚛️');
const newUsername = ref(auth.user?.username || '');
const usernameError = ref('');
const searchQuery = ref('');
const isSearchOpen = ref(false);
const messagesEnd = ref<HTMLElement | null>(null);
const messagesContainer = ref<HTMLElement | null>(null);
const showScrollButton = ref(false);

const onMessagesScroll = () => {
  if (!messagesContainer.value) return;
  const { scrollTop, scrollHeight, clientHeight } = messagesContainer.value;
  showScrollButton.value = scrollHeight - scrollTop - clientHeight > 150;
};

const scrollToBottomSmooth = () => {
  messagesEnd.value?.scrollIntoView({ behavior: 'smooth' });
};

const scrollToMessage = (msgId: string) => {
  const el = document.getElementById(`msg-${msgId}`);
  if (!el) return;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  el.classList.add('highlight-message');
  setTimeout(() => el.classList.remove('highlight-message'), 1500);
};
const isEmojiPickerOpen = ref(false);
const emojiSearch = ref('');
const isChatEmojiPickerOpen = ref(false);
const chatEmojiSearch = ref('');
const mobileView = ref<'list' | 'chat'>('list');

// ===== КОНТЕКСТНОЕ МЕНЮ =====
const contextMenu = ref<{ x: number; y: number; msg: any } | null>(null);
const reactionPicker = ref<{ x: number; y: number; msgId: string } | null>(null);
const quickReactions = ['👍', '❤️', '😂', '😮', '😢', '🔥'];
let longPressTimer: ReturnType<typeof setTimeout> | null = null;

const onMessageLongPress = (event: TouchEvent | MouseEvent, msg: any) => {
  longPressTimer = setTimeout(() => showContextMenu(event, msg), 500);
};

const onMessageLongPressCancel = () => {
  if (longPressTimer) { clearTimeout(longPressTimer); longPressTimer = null; }
};

const showContextMenu = (event: TouchEvent | MouseEvent, msg: any) => {
  event.preventDefault();
  const isTouchEvent = event instanceof TouchEvent;
  const clientX = isTouchEvent ? (event.changedTouches[0]?.clientX ?? 0) : (event as MouseEvent).clientX;
  const clientY = isTouchEvent ? (event.changedTouches[0]?.clientY ?? 0) : (event as MouseEvent).clientY;
  const x = Math.min(clientX, window.innerWidth - 168);
  const y = Math.min(clientY, window.innerHeight - 120);
  contextMenu.value = { x, y, msg };
  reactionPicker.value = null;
};

const closeContextMenu = () => {
  contextMenu.value = null;
  reactionPicker.value = null;
};

const onReplyFromMenu = () => {
  if (!contextMenu.value) return;
  chat.setReply(contextMenu.value.msg);
  closeContextMenu();
};

const onReactFromMenu = () => {
  if (!contextMenu.value) return;
  reactionPicker.value = { x: contextMenu.value.x, y: contextMenu.value.y, msgId: contextMenu.value.msg.id };
  contextMenu.value = null;
};

const sendReaction = async (emoji: string) => {
  if (!reactionPicker.value) return;
  await chat.addReaction(reactionPicker.value.msgId, emoji);
  reactionPicker.value = null;
};

// ===== ТЕМА =====
const isDark = ref(true);

const initTheme = () => {
  const saved = localStorage.getItem('theme');
  if (saved === 'light') {
    isDark.value = false;
    document.documentElement.classList.remove('dark');
  } else {
    isDark.value = true;
    document.documentElement.classList.add('dark');
  }
};

const toggleTheme = () => {
  isDark.value = !isDark.value;
  if (isDark.value) {
    document.documentElement.classList.add('dark');
    localStorage.setItem('theme', 'dark');
  } else {
    document.documentElement.classList.remove('dark');
    localStorage.setItem('theme', 'light');
  }
};

// ===== СТИКЕРЫ =====
const isStickerPickerOpen = ref(false);
const activeStickerPackId = ref<string | null>(null);
const isCreatePackOpen = ref(false);
const newPackName = ref('');
const creatingPack = ref(false);
const uploadingSticker = ref(false);
const stickerPackModal = ref<null | { id: string, name: string, coverUrl?: string, stickers: any[], isAdded: boolean, createdBy: string }>(null);
const stickerPackModalLoading = ref(false);

// Загрузка файлов
const uploadingAttachment = ref(false);

const openImage = (url: string) => window.open(url, '_blank');

const triggerAttachmentInput = () => document.getElementById('attachmentInput')?.click();

const onAttachmentSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (!target.files || !target.files[0]) return;
  uploadingAttachment.value = true;
  try {
    await chat.uploadAttachment(target.files[0]);
  } catch (e) {
    alert('Ошибка при загрузке файла');
  } finally {
    uploadingAttachment.value = false;
    target.value = '';
  }
};

// ===== ГОЛОСОВЫЕ СООБЩЕНИЯ =====
const isRecording = ref(false);
const audioPlayers = ref<Record<string, { playing: boolean; currentTime: number; duration: number; audio: HTMLAudioElement | null }>>({});

const getAudioPlayer = (msgId: string) => {
  if (!audioPlayers.value[msgId]) {
    audioPlayers.value[msgId] = { playing: false, currentTime: 0, duration: 0, audio: null };
  }
  return audioPlayers.value[msgId];
};

const initAudioPlayer = async (msgId: string, url: string) => {
  const player = getAudioPlayer(msgId);
  if (player.audio) return;
  const resolvedUrl = await fetchFileAsBlob(url);
  const audio = new Audio(resolvedUrl);
  audio.addEventListener('loadedmetadata', () => { player.duration = audio.duration; });
  audio.addEventListener('timeupdate', () => { player.currentTime = audio.currentTime; });
  audio.addEventListener('ended', () => { player.playing = false; player.currentTime = 0; });
  player.audio = audio;
  player.duration = audio.duration || 0;
};

const toggleAudio = async (msgId: string, url: string) => {
  await initAudioPlayer(msgId, url);
  const player = audioPlayers.value[msgId];
  if (!player?.audio) return;
  // Останавливаем все остальные
  for (const [id, p] of Object.entries(audioPlayers.value)) {
    if (id !== msgId && p.playing) { p.audio?.pause(); p.playing = false; }
  }
  if (player.playing) {
    player.audio.pause();
    player.playing = false;
  } else {
    player.audio.play();
    player.playing = true;
  }
};

const seekAudio = (msgId: string, event: MouseEvent) => {
  const player = audioPlayers.value[msgId];
  if (!player?.audio || !player.duration) return;
  const bar = event.currentTarget as HTMLElement;
  const ratio = event.offsetX / bar.offsetWidth;
  player.audio.currentTime = ratio * player.duration;
};

const formatAudioTime = (seconds: number): string => {
  if (!seconds || isNaN(seconds)) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
};

// Запись голосового
let mediaRecorder: MediaRecorder | null = null;
let audioChunks: Blob[] = [];

const toggleRecording = async () => {
  if (!chat.activeChat) return;

  if (isRecording.value) {
    // Останавливаем запись
    mediaRecorder?.stop();
    isRecording.value = false;
  } else {
    // Начинаем запись
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      audioChunks = [];

      const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
        ? 'audio/webm;codecs=opus'
        : MediaRecorder.isTypeSupported('audio/webm')
          ? 'audio/webm'
          : 'audio/ogg';

      mediaRecorder = new MediaRecorder(stream, { mimeType });

      mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) audioChunks.push(e.data);
      };

      mediaRecorder.onstop = async () => {
        stream.getTracks().forEach(t => t.stop());
        if (audioChunks.length === 0) return;
        const blob = new Blob(audioChunks, { type: mimeType });
        if (blob.size < 1000) return; // игнорируем слишком короткие
        const ext = mimeType.includes('ogg') ? 'ogg' : 'webm';
        const file = new File([blob], `voice_${Date.now()}.${ext}`, { type: mimeType });
        uploadingAttachment.value = true;
        try {
          await chat.uploadAttachment(file);
        } finally {
          uploadingAttachment.value = false;
        }
      };

      // timeslice 100ms — данные пишутся каждые 100мс, не только в конце
      mediaRecorder.start(100);
      isRecording.value = true;
    } catch {
      alert('Нет доступа к микрофону');
    }
  }
};

// Спиннеры
const chatsLoading = ref(false);
const messagesLoading = ref(false);

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

watch(currentMessages, (msgs) => {
  scrollToBottom();
  // Резолвим все attachmentUrl в сообщениях
  for (const msg of msgs) {
    if (msg.attachmentUrl) resolveUrl(msg.attachmentUrl);
  }
}, { deep: true });

onMounted(async () => {
  initTheme();
  chatsLoading.value = true;
  await chat.connect();
  await chat.loadChats();
  chatsLoading.value = false;
  if (window.innerWidth >= 768) {
    const general = chat.chats.find(c => c.type === 0);
    if (general) await chat.openChat(general);
  }
});

onUnmounted(() => chat.disconnect());

const openChat = async (c: any) => {
  messagesLoading.value = true;
  await chat.openChat(c);
  messagesLoading.value = false;
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
  <div class="flex h-screen bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 overflow-hidden font-sans select-none transition-colors duration-300">

    <!-- ===== САЙДБАР ===== -->
    <aside :class="[
      'flex flex-col bg-slate-50 dark:bg-slate-950/50 backdrop-blur-xl border-r border-slate-200 dark:border-slate-800',
      'md:relative md:w-80 md:flex',
      mobileView === 'list' ? 'absolute inset-0 z-10 flex w-full' : 'hidden md:flex'
    ]">
      <!-- Шапка -->
      <div class="p-4 border-b border-slate-200 dark:border-slate-800 flex justify-between items-center bg-white/80 dark:bg-slate-950/30 shrink-0">
        <div class="flex items-center gap-3 overflow-hidden">
          <div class="w-2.5 h-2.5 bg-green-500 rounded-full animate-pulse shrink-0 shadow-[0_0_10px_rgba(34,197,94,0.5)]"></div>
          <div class="flex items-center gap-1.5 min-w-0">
            <span class="text-lg leading-none">{{ auth.user?.emojiPrefix || '⚛️' }}</span>
            <h2 class="text-base font-black tracking-tight truncate uppercase italic text-slate-900 dark:text-white">
              {{ auth.user?.username || 'User' }}
            </h2>
          </div>
        </div>
        <div class="flex items-center gap-2 shrink-0">
          <!-- Переключатель темы -->
          <button @click="toggleTheme"
                  class="w-10 h-10 rounded-xl bg-slate-100 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 flex items-center justify-center hover:bg-slate-200 dark:hover:bg-slate-700 transition-all active:scale-90"
                  :title="isDark ? 'Светлая тема' : 'Тёмная тема'">
            <!-- Солнце — показываем когда тёмная тема активна (предлагаем переключиться в светлую) -->
            <svg v-if="isDark" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707M17.657 17.657l-.707-.707M6.343 6.343l-.707-.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/>
            </svg>
            <!-- Луна — показываем когда светлая тема активна (предлагаем переключиться в тёмную) -->
            <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/>
            </svg>
          </button>

          <div @click="openProfile" class="relative group cursor-pointer transition-transform active:scale-90">
            <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl"
                 class="w-10 h-10 rounded-xl border-2 border-purple-500 object-cover">
            <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center font-bold border-2 border-white/10 text-white">
              {{ auth.user?.username?.[0]?.toUpperCase() }}
            </div>
          </div>

          <button v-if="auth.user?.role === 1" @click="router.push('/admin')"
                  class="w-10 h-10 rounded-xl bg-purple-600/20 border border-purple-500/30 flex items-center justify-center hover:bg-purple-600/40 transition-all active:scale-90">
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Список чатов -->
      <div class="flex-1 overflow-y-auto">
        <div v-if="chatsLoading" class="flex flex-col items-center justify-center h-full gap-3">
          <div class="w-7 h-7 border-2 border-purple-500 border-t-transparent rounded-full animate-spin"></div>
          <p class="text-xs text-slate-400 dark:text-slate-500">Загрузка чатов...</p>
        </div>
        <div v-else-if="chat.chats.length === 0" class="flex flex-col items-center justify-center h-full opacity-20 px-10 text-center">
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
               :class="['flex items-center gap-3 px-4 py-3 cursor-pointer transition-all active:bg-slate-200/50 dark:active:bg-slate-700/50 hover:bg-slate-100 dark:hover:bg-slate-800/50',
                 chat.activeChat?.id === c.id ? 'bg-purple-50 dark:bg-purple-600/20 border-r-2 border-purple-500' : '']">
            <div class="relative shrink-0">
              <img v-if="c.avatarUrl" :src="c.avatarUrl" class="w-12 h-12 rounded-xl object-cover">
              <div v-else class="w-12 h-12 rounded-xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-xl">
                {{ c.name?.[0]?.toUpperCase() }}
              </div>
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-1.5">
                <svg viewBox="0 0 16 16" class="w-3 h-3 text-purple-400 shrink-0 fill-current">
                  <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm0 1a5 5 0 0 0-5 5h10a5 5 0 0 0-5-5z"/>
                </svg>
                <p class="font-bold text-sm truncate text-purple-600 dark:text-purple-300">{{ c.name || 'Контакт' }}</p>
              </div>
              <p class="text-xs text-slate-400 dark:text-slate-500 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
            </div>
            <span v-if="c.unreadCount > 0"
                  class="shrink-0 min-w-5 h-5 px-1 bg-purple-500 text-white text-xs font-bold rounded-full flex items-center justify-center">
              {{ c.unreadCount > 99 ? '99+' : c.unreadCount }}
            </span>
            <span class="text-slate-300 dark:text-slate-600 md:hidden flex items-center">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" class="w-5 h-5" fill="none">
                <path d="M9 6L15 12L9 18" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
            </span>
          </div>
        </template>

        <!-- Раздел ЧАТЫ -->
        <template v-if="chat.otherChats.length > 0">
          <div class="px-4 pt-3 pb-1 flex items-center gap-2">
            <span class="text-[10px] font-black uppercase tracking-widest text-slate-400 dark:text-slate-500">Чаты</span>
            <div class="flex-1 h-px bg-slate-200 dark:bg-slate-700/50"></div>
          </div>
          <div v-for="c in chat.otherChats" :key="c.id" @click="openChat(c)"
               :class="['flex items-center gap-3 px-4 py-3 cursor-pointer transition-all active:bg-slate-200/50 dark:active:bg-slate-700/50 hover:bg-slate-100 dark:hover:bg-slate-800/50',
                 chat.activeChat?.id === c.id ? 'bg-purple-50 dark:bg-purple-600/20 border-r-2 border-purple-500' : '']">
            <div class="relative shrink-0">
              <img v-if="c.avatarUrl && c.type === 1" :src="c.avatarUrl" class="w-12 h-12 rounded-xl object-cover">
              <div v-else class="w-12 h-12 rounded-xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-xl">
                {{ c.type === 0 ? '🌐' : c.name?.[0]?.toUpperCase() }}
              </div>
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-bold text-sm truncate">{{ c.name || 'Чат' }}</p>
              <p class="text-xs text-slate-400 dark:text-slate-500 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
            </div>
            <span v-if="c.unreadCount > 0"
                  class="shrink-0 min-w-5 h-5 px-1 bg-purple-500 text-white text-xs font-bold rounded-full flex items-center justify-center">
              {{ c.unreadCount > 99 ? '99+' : c.unreadCount }}
            </span>
            <span class="text-slate-300 dark:text-slate-600 md:hidden flex items-center">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" class="w-5 h-5" fill="none">
                <path d="M9 6L15 12L9 18" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
            </span>
          </div>
        </template>
      </div>

      <!-- Футер сайдбара -->
      <div class="p-4 border-t border-slate-200 dark:border-slate-800 shrink-0 space-y-2">
        <button @click="openAddContacts"
                class="w-full flex items-center gap-2 px-4 py-2.5 rounded-xl bg-purple-600/10 border border-purple-500/20 hover:bg-purple-600/20 transition-all active:scale-95 text-purple-500 dark:text-purple-400 text-sm font-bold">
          <svg viewBox="0 0 24 24" class="w-4 h-4 fill-current shrink-0">
            <path d="M19 11h-6V5a1 1 0 0 0-2 0v6H5a1 1 0 0 0 0 2h6v6a1 1 0 0 0 2 0v-6h6a1 1 0 0 0 0-2z"/>
          </svg>
          Добавить контакты...
        </button>
        <div class="relative">
          <input v-model="searchQuery" @input="onSearch" type="text" placeholder="Найти пользователя..."
                 class="w-full bg-white dark:bg-slate-900/50 border border-slate-200 dark:border-slate-800 rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-2 focus:ring-purple-500/50 transition-all select-text text-slate-900 dark:text-slate-100 placeholder:text-slate-400 dark:placeholder:text-slate-600">
          <div v-if="isSearchOpen"
               class="absolute bottom-full mb-2 left-0 right-0 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl overflow-hidden shadow-2xl max-h-48 overflow-y-auto">
            <div v-for="user in chat.users" :key="user.id" @click="startDm(user.id)"
                 class="flex items-center gap-3 px-4 py-3 hover:bg-slate-100 dark:hover:bg-slate-800 active:bg-slate-200 dark:active:bg-slate-700 cursor-pointer transition-all">
              <div :class="['w-2 h-2 rounded-full shrink-0', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-300 dark:bg-slate-600']"></div>
              <span class="text-sm">{{ user.emojiPrefix || '' }} {{ user.username }}</span>
            </div>
            <div v-if="chat.users.length === 0" class="px-4 py-2 text-xs text-slate-400 dark:text-slate-500 text-center">Не найдено</div>
          </div>
        </div>
      </div>
    </aside>

    <!-- ===== ОСНОВНАЯ ОБЛАСТЬ ===== -->
    <main :class="['flex-1 flex flex-col bg-slate-50 dark:bg-slate-900 relative', mobileView === 'list' ? 'hidden md:flex' : 'flex']">
      <div class="absolute inset-0 opacity-[0.03] pointer-events-none bg-[url('https://www.transparenttextures.com/patterns/carbon-fibre.png')]"></div>

      <header class="px-4 py-3 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between bg-white/80 dark:bg-slate-900/80 backdrop-blur-md z-10 shrink-0">
        <div class="flex items-center gap-2">
          <button @click="goBackToList" class="md:hidden w-9 h-9 flex items-center justify-center rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 active:bg-slate-200 dark:active:bg-slate-700 transition-all">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
              <defs><linearGradient id="g" x1="0" y1="0" x2="24" y2="24">
              <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
              <path d="M15 6L9 12L15 18" stroke="url(#g)" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <div v-if="chat.activeChat" class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-lg bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-base shrink-0">
              {{ chat.activeChat.type === 0 ? '🌐' : chat.activeChat.name?.[0]?.toUpperCase() }}
            </div>
            <div class="flex items-center gap-1.5">
              <p class="font-bold text-sm" :class="chat.activeChat.isContact ? 'text-purple-600 dark:text-purple-300' : ''">
                {{ chat.activeChat.name || 'Чат' }}
              </p>
              <svg v-if="chat.activeChat.isContact" viewBox="0 0 16 16" class="w-3.5 h-3.5 text-purple-400 fill-current">
                <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm0 1a5 5 0 0 0-5 5h10a5 5 0 0 0-5-5z"/>
              </svg>
            </div>
          </div>
          <div v-else class="flex items-center gap-2">
            <div class="w-2 h-2 rounded-full bg-slate-300 dark:bg-slate-600"></div>
            <span class="font-bold tracking-widest text-xs uppercase opacity-70">Выберите чат</span>
          </div>
        </div>
        <button @click="handleLogout"
                class="px-3 py-2 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-500 dark:text-red-400 text-xs font-bold transition-all border border-red-500/20 active:scale-95">
          Выйти
        </button>
      </header>

      <!-- Лента сообщений -->
      <div ref="messagesContainer" @scroll="onMessagesScroll" class="flex-1 px-3 py-4 overflow-y-auto z-10 space-y-3 select-text relative">

        <div v-if="messagesLoading" class="flex h-full items-center justify-center">
          <div class="flex flex-col items-center gap-3 opacity-50">
            <div class="w-8 h-8 border-2 border-purple-500 border-t-transparent rounded-full animate-spin"></div>
            <p class="text-xs text-slate-400 dark:text-slate-500">Загрузка сообщений...</p>
          </div>
        </div>

        <div v-else-if="!chat.activeChat" class="flex h-full items-center justify-center opacity-20">
          <div class="text-center">
            <p class="text-4xl mb-2">💬</p>
            <p class="text-sm italic font-medium">Выберите чат</p>
          </div>
        </div>

        <template v-else>
          <div v-if="currentMessages.length === 0" class="flex justify-center pt-10 opacity-30">
            <p class="text-sm italic">Сообщений пока нет. Будь первым!</p>
          </div>

          <div v-for="msg in currentMessages" :key="msg.id" :id="`msg-${msg.id}`"
               :class="['flex gap-2 items-end', msg.userId === auth.user?.id ? 'flex-row-reverse' : '']">
            <img v-if="msg.avatarUrl" :src="msg.avatarUrl" class="w-7 h-7 rounded-lg object-cover shrink-0 mb-5">
            <div v-else class="w-7 h-7 rounded-lg bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center text-xs font-bold shrink-0 mb-5 text-white">
              {{ msg.username?.[0]?.toUpperCase() }}
            </div>

            <div :class="['flex flex-col gap-0.5 max-w-xs sm:max-w-sm', msg.userId === auth.user?.id ? 'items-end' : 'items-start']">
              <span class="text-[10px] text-slate-400 dark:text-slate-500 px-1">{{ msg.emojiPrefix }} {{ msg.username }} · {{ formatTime(msg.sentAt) }}</span>

              <!-- Блок сообщения с контекстным меню -->
              <div
                @contextmenu.prevent="showContextMenu($event, msg)"
                @touchstart="onMessageLongPress($event, msg)"
                @touchend="onMessageLongPressCancel"
                @touchmove="onMessageLongPressCancel"
                class="relative">

                <!-- Превью ответа -->
                <div v-if="msg.replyToUsername"
                     @click="msg.replyToMessageId && scrollToMessage(msg.replyToMessageId)"
                     :class="['mb-1 px-2 py-1 rounded-xl border-l-2 border-purple-400 text-xs opacity-70 max-w-full cursor-pointer hover:opacity-100 transition-opacity',
                       msg.userId === auth.user?.id ? 'bg-white/10' : 'bg-slate-300/50 dark:bg-slate-700/50']">
                  <p class="font-bold text-purple-400 truncate">{{ msg.replyToUsername }}</p>
                  <p class="truncate text-slate-600 dark:text-slate-300">{{ msg.replyToContent }}</p>
                </div>

                <!-- Стикер -->
                <div v-if="msg.type === 'Sticker' && msg.attachmentUrl"
                     @click="msg.packId && onStickerClick(msg.packId)"
                     class="cursor-pointer active:scale-95 transition-transform relative">
                  <img :src="msg.attachmentUrl" class="w-32 h-32 object-contain rounded-2xl" draggable="false" oncontextmenu="return false">
                  <span v-if="msg.userId === auth.user?.id" class="absolute bottom-1 right-1 opacity-70">
                    <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                    <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  </span>
                </div>

                <!-- Изображение -->
                <div v-else-if="msg.type === 'Image' && msg.attachmentUrl" class="relative">
                  <img v-if="getResolvedUrl(msg.attachmentUrl)"
                       :src="getResolvedUrl(msg.attachmentUrl)"
                       class="max-w-xs rounded-2xl object-cover cursor-pointer"
                       draggable="false" oncontextmenu="return false"
                       @click="openImage(getResolvedUrl(msg.attachmentUrl))">
                  <div v-else class="max-w-xs h-32 rounded-2xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center">
                    <div class="w-5 h-5 border-2 border-purple-400 border-t-transparent rounded-full animate-spin"></div>
                  </div>
                  <span v-if="msg.userId === auth.user?.id" class="absolute bottom-1 right-1 opacity-70">
                    <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                    <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  </span>
                </div>

                <!-- Видео -->
                <div v-else-if="msg.type === 'Video' && msg.attachmentUrl" class="relative">
                  <video v-if="getResolvedUrl(msg.attachmentUrl)"
                         :src="getResolvedUrl(msg.attachmentUrl)"
                         controls class="max-w-xs rounded-2xl"></video>
                  <div v-else class="max-w-xs h-32 rounded-2xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center">
                    <div class="w-5 h-5 border-2 border-purple-400 border-t-transparent rounded-full animate-spin"></div>
                  </div>
                  <span v-if="msg.userId === auth.user?.id" class="absolute bottom-1 right-1 opacity-70">
                    <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                    <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  </span>
                </div>

                <!-- Голосовое -->
                <div v-else-if="msg.type === 'Voice' && msg.attachmentUrl"
                     :class="['flex items-center gap-3 px-3 py-2.5 rounded-2xl w-64',
                       msg.userId === auth.user?.id
                         ? 'bg-gradient-to-r from-purple-600 to-indigo-600'
                         : 'bg-slate-200 dark:bg-slate-800']"
                     @click.once="initAudioPlayer(msg.id, msg.attachmentUrl)">
                  <!-- Кнопка play/pause -->
                  <button @click="toggleAudio(msg.id, msg.attachmentUrl)"
                          :class="['w-9 h-9 rounded-full flex items-center justify-center shrink-0 transition-all active:scale-90',
                            msg.userId === auth.user?.id ? 'bg-white/20 hover:bg-white/30' : 'bg-purple-500 hover:bg-purple-600']">
                    <svg v-if="!getAudioPlayer(msg.id).playing" viewBox="0 0 24 24" class="w-4 h-4 fill-white ml-0.5">
                      <path d="M8 5v14l11-7z"/>
                    </svg>
                    <svg v-else viewBox="0 0 24 24" class="w-4 h-4 fill-white">
                      <path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/>
                    </svg>
                  </button>
                  <!-- Прогресс бар + время -->
                  <div class="flex-1 min-w-0">
                    <div class="relative h-1.5 rounded-full cursor-pointer mb-1"
                         :class="msg.userId === auth.user?.id ? 'bg-white/20' : 'bg-slate-300 dark:bg-slate-600'"
                         @click="seekAudio(msg.id, $event)">
                      <div class="h-full rounded-full transition-all"
                           :class="msg.userId === auth.user?.id ? 'bg-white' : 'bg-purple-500'"
                           :style="{ width: getAudioPlayer(msg.id).duration ? (getAudioPlayer(msg.id).currentTime / getAudioPlayer(msg.id).duration * 100) + '%' : '0%' }">
                      </div>
                    </div>
                    <span :class="['text-[10px]', msg.userId === auth.user?.id ? 'text-white/70' : 'text-slate-500 dark:text-slate-400']">
                      {{ getAudioPlayer(msg.id).playing || getAudioPlayer(msg.id).currentTime > 0
                          ? formatAudioTime(getAudioPlayer(msg.id).currentTime)
                          : formatAudioTime(getAudioPlayer(msg.id).duration) }}
                    </span>
                  </div>
                  <!-- Галочки -->
                  <span v-if="msg.userId === auth.user?.id" class="opacity-70 shrink-0">
                    <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                    <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  </span>
                </div>

                <!-- Обычное сообщение -->
                <div v-else :class="[
                  'px-3 py-2 rounded-2xl text-sm break-words leading-relaxed',
                  msg.userId === auth.user?.id
                    ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white rounded-br-sm'
                    : 'bg-slate-200 dark:bg-slate-800 text-slate-900 dark:text-slate-100 rounded-bl-sm'
                ]">
                  {{ msg.content }}
                  <span v-if="msg.userId === auth.user?.id" class="inline-flex items-center ml-2 opacity-70 translate-y-0.5">
                    <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                    <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  </span>
                </div>

                <!-- Реакции -->
                <div v-if="msg.reactions && msg.reactions.length > 0"
                     :class="['flex flex-wrap gap-1 mt-1', msg.userId === auth.user?.id ? 'justify-end' : 'justify-start']">
                  <button v-for="r in msg.reactions" :key="r.emoji"
                          @click="chat.addReaction(msg.id, r.emoji)"
                          :class="['flex items-center gap-1 px-2 py-0.5 rounded-full text-xs transition-all active:scale-90 border',
                            r.userIds.some(id => id.toLowerCase() === auth.user?.id?.toLowerCase())
                              ? 'bg-purple-100 dark:bg-purple-600/30 border-purple-400 text-purple-700 dark:text-purple-300'
                              : 'bg-slate-100 dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300']">
                    <span>{{ r.emoji }}</span>
                    <span class="font-bold">{{ r.count }}</span>
                  </button>
                </div>

              </div>
            </div>
          </div>
        </template>

        <div ref="messagesEnd"></div>
      </div>

      <!-- Кнопка "вниз" -->
      <Transition name="fade">
        <button v-if="showScrollButton" @click="scrollToBottomSmooth"
                class="absolute bottom-20 right-4 z-20 w-10 h-10 rounded-full bg-purple-600 hover:bg-purple-500 text-white shadow-lg flex items-center justify-center transition-all active:scale-90">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 9l-7 7-7-7"/>
          </svg>
        </button>
      </Transition>

      <!-- Футер -->
      <footer class="p-3 bg-white/80 dark:bg-slate-950/80 backdrop-blur-md border-t border-slate-200 dark:border-slate-800 z-10 shrink-0 relative">
        <Transition name="picker">
          <div v-if="isStickerPickerOpen" class="mb-2 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden shadow-2xl" @click.stop>
            <div class="flex h-64">
              <div class="w-14 bg-slate-50 dark:bg-slate-900 border-r border-slate-200 dark:border-slate-800 flex flex-col items-center py-2 gap-2 overflow-y-auto shrink-0">
                <button v-for="pack in chat.stickerPacks" :key="pack.id" @click="selectPack(pack.id)"
                        :class="['w-10 h-10 rounded-xl overflow-hidden border-2 transition-all active:scale-90',
                          activePack?.id === pack.id ? 'border-purple-500' : 'border-transparent']">
                  <img v-if="pack.coverUrl" :src="pack.coverUrl" class="w-full h-full object-cover">
                  <div v-else class="w-full h-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-xs font-bold">{{ pack.name[0] }}</div>
                </button>
                <button @click="isCreatePackOpen = !isCreatePackOpen; activeStickerPackId = null"
                        :class="['w-10 h-10 rounded-xl border-2 flex items-center justify-center transition-all active:scale-90',
                          isCreatePackOpen ? 'border-purple-500 bg-purple-600/20 text-purple-400' : 'border-slate-200 dark:border-slate-700 text-slate-400 dark:text-slate-500 hover:border-slate-300 dark:hover:border-slate-600']">
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                  </svg>
                </button>
              </div>
              <div class="flex-1 overflow-y-auto">
                <div v-if="isCreatePackOpen" class="p-4 space-y-3">
                  <p class="text-xs font-bold text-slate-500 uppercase">Новый пак стикеров</p>
                  <input v-model="newPackName" type="text" placeholder="Название пака..."
                         class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-sm outline-none focus:ring-1 focus:ring-purple-500 select-text text-slate-900 dark:text-slate-100">
                  <button @click="createPack" :disabled="creatingPack || !newPackName.trim()"
                          class="w-full bg-gradient-to-r from-purple-600 to-indigo-600 text-white font-bold py-2 rounded-xl text-sm active:scale-95 transition-all disabled:opacity-50">
                    {{ creatingPack ? 'Создаём...' : 'Создать' }}
                  </button>
                </div>
                <div v-else-if="activePack">
                  <div class="flex items-center justify-between px-3 pt-3 pb-2 border-b border-slate-200 dark:border-slate-800">
                    <p class="text-xs font-bold text-slate-500 truncate">{{ activePack.name }}</p>
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
                            class="w-full aspect-square rounded-xl overflow-hidden hover:bg-slate-100 dark:hover:bg-slate-700 active:scale-90 transition-all p-1">
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
          <div v-if="isChatEmojiPickerOpen" class="mb-2 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden shadow-2xl" @click.stop>
            <div class="p-2 border-b border-slate-200 dark:border-slate-800">
              <input v-model="chatEmojiSearch" type="text" placeholder="Поиск эмодзи..."
                     class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text text-slate-900 dark:text-slate-100">
            </div>
            <div class="max-h-48 overflow-y-auto p-2 space-y-3">
              <div v-for="cat in filteredChatEmojis" :key="cat.label">
                <p class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                <div class="flex flex-wrap gap-0.5">
                  <button v-for="emoji in cat.emojis" :key="emoji" @click="insertEmoji(emoji)"
                          class="w-9 h-9 flex items-center justify-center rounded-lg text-xl hover:bg-slate-100 dark:hover:bg-slate-700 active:scale-90 transition-all">
                    {{ emoji }}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </Transition>

        <div class="flex items-end gap-2 bg-slate-100 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700/50 p-1.5 rounded-2xl focus-within:border-purple-500/50 transition-all">

          <!-- Превью ответа -->
          <Transition name="picker">
            <div v-if="chat.replyingTo" class="absolute bottom-full left-0 right-0 mb-1 mx-3">
              <div class="flex items-center gap-2 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 shadow-lg">
                <div class="w-0.5 h-8 bg-purple-500 rounded-full shrink-0"></div>
                <div class="flex-1 min-w-0">
                  <p class="text-xs font-bold text-purple-500">{{ chat.replyingTo.username }}</p>
                  <p class="text-xs text-slate-500 dark:text-slate-400 truncate">{{ chat.replyingTo.type === 'Text' || !chat.replyingTo.type ? chat.replyingTo.content : chat.replyingTo.type === 'Image' ? '🖼️ Изображение' : chat.replyingTo.type === 'Video' ? '📹 Видео' : '🎭 Стикер' }}</p>
                </div>
                <button @click="chat.setReply(null)" class="shrink-0 w-6 h-6 flex items-center justify-center rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-400 transition-all">
                  <svg viewBox="0 0 24 24" class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2.5">
                    <path d="M18 6L6 18M6 6l12 12" stroke-linecap="round"/>
                  </svg>
                </button>
              </div>
            </div>
          </Transition>

          <button @click="triggerAttachmentInput" :disabled="!chat.activeChat || uploadingAttachment"
                  :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                    !chat.activeChat ? 'opacity-40 cursor-not-allowed' : 'hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400']">
            <div v-if="uploadingAttachment" class="w-6 h-6 flex items-center justify-center">
              <div class="w-4 h-4 border-2 border-purple-400 border-t-transparent rounded-full animate-spin"></div>
            </div>
            <svg v-else xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none">
              <defs><linearGradient id="ga" x1="0" y1="0" x2="24" y2="24">
              <stop offset="0%" stop-color="#7C5CFF"/><stop offset="100%" stop-color="#37C8FF"/></linearGradient></defs>
              <path d="M21 11.5L12.5 20C10.3 22.2 6.7 22.2 4.5 20C2.3 17.8 2.3 14.2 4.5 12L13 3.5C14.4 2.1 16.6 2.1 18 3.5C19.4 4.9 19.4 7.1 18 8.5L9.5 17C8.8 17.7 7.7 17.7 7 17C6.3 16.3 6.3 15.2 7 14.5L14.5 7"
              stroke="url(#ga)" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <input id="attachmentInput" type="file" class="hidden"
                 accept="image/png,image/jpeg,image/gif,image/webp,video/mp4,video/webm"
                 @change="onAttachmentSelected">

          <button @click.stop="toggleStickerPicker" :disabled="!chat.activeChat"
                  :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                    isStickerPickerOpen ? 'bg-purple-600/30 text-purple-500 dark:text-purple-300' : 'hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400',
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
                    isChatEmojiPickerOpen ? 'bg-purple-600/30 text-purple-500 dark:text-purple-300' : 'hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400']">
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
                    class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-400 dark:placeholder:text-slate-600 select-text disabled:opacity-40 resize-none max-h-32 overflow-y-auto text-slate-900 dark:text-slate-100"
                    style="field-sizing: content;"></textarea>
          <button @click="sendMessage" :disabled="!chat.activeChat || !messageInput.trim()"
                  class="bg-gradient-to-r from-purple-600 to-indigo-600 text-white px-4 py-2.5 rounded-xl shadow-lg active:scale-95 transition-all disabled:opacity-40 disabled:cursor-not-allowed shrink-0 self-end">
            <svg viewBox="0 0 24 24" class="w-6 h-6 fill-white"><path d="M3 12L21 4L14 20L11 13L3 12Z"/></svg>
          </button>

          <!-- Кнопка микрофона — показывается когда инпут пустой -->
          <button v-if="!messageInput.trim() && chat.activeChat"
                  @click="toggleRecording"
                  :class="['px-3 py-2.5 rounded-xl shadow-lg transition-all shrink-0 self-end active:scale-95',
                    isRecording
                      ? 'bg-red-500 animate-pulse'
                      : 'bg-gradient-to-r from-purple-600 to-indigo-600']">
            <svg v-if="!isRecording" viewBox="0 0 24 24" class="w-6 h-6 fill-white">
              <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/>
              <path d="M19 10v2a7 7 0 0 1-14 0v-2H3v2a9 9 0 0 0 8 8.94V23h-3v2h8v-2h-3v-2.06A9 9 0 0 0 21 12v-2h-2z"/>
            </svg>
            <svg v-else viewBox="0 0 24 24" class="w-6 h-6 fill-white">
              <rect x="6" y="6" width="12" height="12" rx="2"/>
            </svg>
          </button>
        </div>
      </footer>
    </main>

    <!-- ===== МОДАЛКА ДОБАВЛЕНИЯ КОНТАКТОВ ===== -->
    <Transition name="fade">
      <div v-if="isAddContactsOpen" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4">
        <div @click="isAddContactsOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div class="p-5 border-b border-slate-200 dark:border-slate-800">
            <p class="font-black text-lg">Добавить контакты</p>
            <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">Выбери пользователей</p>
          </div>
          <div class="px-4 pt-3">
            <input v-model="contactSearch" type="text" placeholder="Поиск..."
                   class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-1 focus:ring-purple-500 select-text text-slate-900 dark:text-slate-100">
          </div>
          <div class="max-h-64 overflow-y-auto p-2 mt-2">
            <div v-if="chat.allUsers.length === 0" class="flex items-center justify-center py-8 opacity-30">
              <p class="text-sm">Загрузка...</p>
            </div>
            <div v-for="user in filteredAllUsers" :key="user.id"
                 @click="toggleSelectUser(user.id)"
                 class="flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer transition-all hover:bg-slate-100 dark:hover:bg-slate-800 active:bg-slate-200 dark:active:bg-slate-700">
              <img v-if="user.avatarUrl" :src="user.avatarUrl" class="w-10 h-10 rounded-xl object-cover shrink-0">
              <div v-else class="w-10 h-10 rounded-xl bg-gradient-to-tr from-purple-600 to-indigo-600 flex items-center justify-center text-sm font-bold shrink-0 text-white">
                {{ user.username?.[0]?.toUpperCase() }}
              </div>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-bold truncate">{{ user.emojiPrefix || '' }} {{ user.username }}</p>
                <div class="flex items-center gap-1 mt-0.5">
                  <div :class="['w-1.5 h-1.5 rounded-full', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-300 dark:bg-slate-600']"></div>
                  <p class="text-xs text-slate-400 dark:text-slate-500">{{ isOnline(user.id) ? 'онлайн' : 'оффлайн' }}</p>
                </div>
              </div>
              <div :class="['w-6 h-6 rounded-lg border-2 flex items-center justify-center transition-all shrink-0',
                selectedUserIds.has(user.id) ? 'bg-purple-600 border-purple-600' : 'border-slate-300 dark:border-slate-600']">
                <svg v-if="selectedUserIds.has(user.id)" viewBox="0 0 16 16" class="w-3.5 h-3.5 fill-white">
                  <path d="M13.5 3.5L6 11L2.5 7.5L1.5 8.5L6 13L14.5 4.5L13.5 3.5Z"/>
                </svg>
              </div>
            </div>
          </div>
          <div class="p-4 border-t border-slate-200 dark:border-slate-800 flex gap-3">
            <button @click="isAddContactsOpen = false"
                    class="flex-1 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-900 dark:text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">
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
        <div class="relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div v-if="stickerPackModalLoading" class="flex items-center justify-center py-16">
            <div class="w-8 h-8 border-2 border-purple-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
          <div v-else-if="stickerPackModal">
            <div class="p-5 border-b border-slate-200 dark:border-slate-800">
              <p class="font-black text-lg">{{ stickerPackModal.name }}</p>
              <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">Автор: {{ stickerPackModal.createdBy }}</p>
            </div>
            <div class="p-3 grid grid-cols-4 gap-2 max-h-56 overflow-y-auto">
              <div v-for="s in stickerPackModal.stickers" :key="s.id" class="aspect-square rounded-xl overflow-hidden bg-slate-100 dark:bg-slate-800 p-1">
                <img :src="s.url" class="w-full h-full object-contain" draggable="false" oncontextmenu="return false">
              </div>
            </div>
            <div class="p-4 border-t border-slate-200 dark:border-slate-800">
              <button @click="togglePackFromModal"
                      :class="['w-full font-bold py-3 rounded-xl text-sm active:scale-95 transition-all',
                        stickerPackModal.isAdded
                          ? 'bg-red-500/10 border border-red-500/20 text-red-500 dark:text-red-400 hover:bg-red-500/20'
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
        <div class="relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div class="h-20 bg-gradient-to-r from-purple-600 to-indigo-700"></div>
          <div class="px-6 pb-8">
            <div class="relative -mt-10 mb-4 flex justify-center">
              <div @click="triggerFileInput" class="relative group cursor-pointer">
                <img v-if="currentAvatar" :src="currentAvatar" class="w-20 h-20 rounded-2xl border-4 border-white dark:border-slate-900 object-cover shadow-xl">
                <div v-else class="w-20 h-20 rounded-2xl border-4 border-white dark:border-slate-900 bg-slate-200 dark:bg-slate-800 flex items-center justify-center text-2xl font-bold uppercase">
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
                <label class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase ml-2 mb-1 block">Никнейм</label>
                <input v-model="newUsername" type="text" placeholder="username" maxlength="32" @input="usernameError = ''"
                       :class="['w-full bg-slate-100 dark:bg-slate-800/50 border rounded-xl px-4 py-3 text-sm outline-none focus:ring-1 transition-all select-text text-slate-900 dark:text-slate-100',
                         usernameError ? 'border-red-500/70 focus:ring-red-500' : 'border-slate-200 dark:border-slate-700 focus:ring-purple-500']">
                <p v-if="usernameError" class="text-[10px] text-red-400 ml-2 mt-1">{{ usernameError }}</p>
              </div>
              <div>
                <label class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase ml-2 mb-1 block">Префикс (эмодзи)</label>
                <div class="flex gap-2">
                  <input v-model="userPrefix" type="text" placeholder="🚀" maxlength="2"
                         class="flex-1 bg-slate-100 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-xl px-4 py-3 text-center text-2xl outline-none focus:ring-1 focus:ring-purple-500 select-text">
                  <button @click.stop="isEmojiPickerOpen = !isEmojiPickerOpen"
                          :class="['px-4 rounded-xl border text-lg transition-all', isEmojiPickerOpen ? 'bg-purple-600/30 border-purple-500' : 'bg-slate-100 dark:bg-slate-800/50 border-slate-200 dark:border-slate-700']">
                    🙂
                  </button>
                </div>
                <Transition name="picker">
                  <div v-if="isEmojiPickerOpen" class="mt-2 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden" @click.stop>
                    <div class="p-2 border-b border-slate-200 dark:border-slate-800">
                      <input v-model="emojiSearch" type="text" placeholder="Поиск..."
                             class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text text-slate-900 dark:text-slate-100">
                    </div>
                    <div class="max-h-44 overflow-y-auto p-2 space-y-3">
                      <div v-for="cat in filteredEmojis" :key="cat.label">
                        <p class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                        <div class="flex flex-wrap gap-0.5">
                          <button v-for="emoji in cat.emojis" :key="emoji" @click="selectEmoji(emoji)"
                                  :class="['w-9 h-9 flex items-center justify-center rounded-lg text-xl transition-all active:scale-90',
                                    userPrefix === emoji ? 'bg-purple-600/40 ring-1 ring-purple-500' : 'hover:bg-slate-100 dark:hover:bg-slate-700']">
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
                      class="flex-1 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-900 dark:text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">
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

    <!-- ===== КОНТЕКСТНОЕ МЕНЮ ===== -->
    <Transition name="fade">
      <div v-if="contextMenu" class="fixed inset-0 z-50" @click="closeContextMenu" @contextmenu.prevent="closeContextMenu">
        <div :style="{ position: 'fixed', left: contextMenu.x + 'px', top: contextMenu.y + 'px' }"
             class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-2xl shadow-2xl overflow-hidden w-40"
             @click.stop>
          <button @click="onReplyFromMenu"
                  class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all active:bg-slate-200 dark:active:bg-slate-700">
            <svg viewBox="0 0 24 24" class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M9 17l-5-5 5-5M20 18v-2a4 4 0 0 0-4-4H4" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            Ответить
          </button>
          <button @click="onReactFromMenu"
                  class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all active:bg-slate-200 dark:active:bg-slate-700 border-t border-slate-100 dark:border-slate-800">
            <svg viewBox="0 0 24 24" class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="9" stroke-linecap="round"/>
              <circle cx="9" cy="10" r="1" fill="currentColor" stroke="none"/>
              <circle cx="15" cy="10" r="1" fill="currentColor" stroke="none"/>
              <path d="M8 14c1 2 7 2 8 0" stroke-linecap="round"/>
            </svg>
            Реакция
          </button>
        </div>
      </div>
    </Transition>

    <!-- ===== ПИКЕР РЕАКЦИЙ ===== -->
    <Transition name="fade">
      <div v-if="reactionPicker" class="fixed inset-0 z-50" @click="closeContextMenu">
        <div :style="{ position: 'fixed', left: reactionPicker.x + 'px', top: reactionPicker.y + 'px' }"
             class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-2xl shadow-2xl p-2 flex gap-1"
             @click.stop>
          <button v-for="emoji in quickReactions" :key="emoji"
                  @click="sendReaction(emoji)"
                  class="w-10 h-10 flex items-center justify-center rounded-xl text-xl hover:bg-slate-100 dark:hover:bg-slate-800 active:scale-90 transition-all">
            {{ emoji }}
          </button>
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

.highlight-message {
  animation: highlight 1.5s ease;
}

@keyframes highlight {
  0%   { background-color: transparent; }
  20%  { background-color: rgba(168, 85, 247, 0.25); }
  100% { background-color: transparent; }
}
</style>