<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import { useAuthStore } from '../store/auth';
import { useChatStore } from '../store/chat';
import { useRouter } from 'vue-router';
import { useTheme } from '../composables/useTheme';
import { useBlobCache } from '../composables/useBlobCache';
import { useVoiceRecorder } from '../composables/useVoiceRecorder';
import MessageBubble from '../components/MessageBubble.vue';
import EmojiPicker from '../components/EmojiPicker.vue';
import StickerPicker from '../components/StickerPicker.vue';
import {
  MessageSquare,
  Globe,
  Mic,
  Image as ImageIcon,
  Video,
  Sticker,
  Smile,
  ChevronLeft,
  Paperclip,
  Send,
  Columns2,
  Rows2,
  PanelTop,
  PanelLeft,
  X,
  LayoutGrid,
  Sun,
  Moon,
  LogOut,
  UserPlus,
  Shield,
  Bell,
  BellOff,
  Trash2,
  FileText,
  ArrowDown
} from 'lucide-vue-next';

const auth = useAuthStore();
const chat = useChatStore();
const router = useRouter();

const { isDark, initTheme, toggleTheme } = useTheme();
const { resolveUrl } = useBlobCache();

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
const isEmojiPickerOpen = ref(false);
const emojiSearch = ref('');
const isChatEmojiPickerOpen = ref(false);
const isStickerPickerOpen = ref(false);
const mobileView = ref<'list' | 'chat'>('list');
const chatsLoading = ref(false);
const messagesLoading = ref(false);
const uploadingAttachment = ref(false);

// ===== НАСТРОЙКИ РАСКЛАДКИ И SPLIT VIEW =====
const savedPos = localStorage.getItem('chatListPosition');
const chatListPosition = ref<'top' | 'left'>(savedPos === 'left' ? 'left' : 'top');
const isAllChatsOpen = ref(false);
const splitChatId = ref<string | null>(null);
const splitDirection = ref<'horizontal' | 'vertical'>((localStorage.getItem('splitDirection') as 'horizontal' | 'vertical') || 'horizontal');
const splitMessageInput = ref('');
const isSplitEmojiPickerOpen = ref(false);
const isSplitStickerPickerOpen = ref(false);
const uploadingSplitAttachment = ref(false);

// ===== УВЕДОМЛЕНИЯ (MUTE ПО ЧАТАМ) =====
const mutedChats = ref<Set<string>>(new Set(JSON.parse(localStorage.getItem('mutedChats') || '[]')));
const isChatMuted = (chatId?: string | null) => !!chatId && mutedChats.value.has(chatId);
const toggleChatMute = (chatId?: string | null) => {
  if (!chatId) return;
  if (mutedChats.value.has(chatId)) mutedChats.value.delete(chatId);
  else mutedChats.value.add(chatId);
  mutedChats.value = new Set(mutedChats.value);
  localStorage.setItem('mutedChats', JSON.stringify([...mutedChats.value]));
};

// ===== МОДАЛКА МЕДИА И ФАЙЛОВ =====
const attachmentsModal = ref<{ chatId: string; chatName: string; tab: 'media' | 'files' } | null>(null);
const openAttachmentsModal = (chatId: string, chatName: string, tab: 'media' | 'files') => {
  attachmentsModal.value = { chatId, chatName, tab };
};
const modalAttachmentMessages = computed(() => {
  if (!attachmentsModal.value) return [];
  const msgs = chat.messages[attachmentsModal.value.chatId] || [];
  if (attachmentsModal.value.tab === 'media') {
    return msgs.filter(m => m.type === 'Image' || m.type === 'Video');
  }
  return msgs.filter(m => m.attachmentUrl && m.type !== 'Image' && m.type !== 'Video' && m.type !== 'Voice');
});

const toggleChatListPosition = () => {
  chatListPosition.value = chatListPosition.value === 'top' ? 'left' : 'top';
  localStorage.setItem('chatListPosition', chatListPosition.value);
};

const toggleSplitDirection = () => {
  splitDirection.value = splitDirection.value === 'horizontal' ? 'vertical' : 'horizontal';
  localStorage.setItem('splitDirection', splitDirection.value);
};

const openSplitChat = async (chatObj: any) => {
  if (chatObj.id === chat.activeChat?.id) return;
  splitChatId.value = chatObj.id;
  await chat.loadChatMessages(chatObj.id);
};

const closeSplitChat = () => {
  splitChatId.value = null;
  splitMessageInput.value = '';
  isSplitEmojiPickerOpen.value = false;
  isSplitStickerPickerOpen.value = false;
};

const sendSplitMessage = async () => {
  if (!splitMessageInput.value.trim() || !splitChatId.value) return;
  await chat.sendMessage(splitMessageInput.value, splitChatId.value);
  splitMessageInput.value = '';
};

const handleSplitKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendSplitMessage(); }
};

const handleSendSplitSticker = async (stickerId: string) => {
  if (!splitChatId.value) return;
  await chat.sendSticker(stickerId, splitChatId.value);
  isSplitStickerPickerOpen.value = false;
};

const handleToggleSplitStickerPicker = async () => {
  isSplitStickerPickerOpen.value = !isSplitStickerPickerOpen.value;
  isSplitEmojiPickerOpen.value = false;
  if (isSplitStickerPickerOpen.value && !chat.stickerPacksLoaded) await chat.loadMyPacks();
};

const triggerSplitAttachmentInput = () => document.getElementById('splitAttachmentInput')?.click();
const onSplitAttachmentSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (!target.files || !target.files[0] || !splitChatId.value) return;
  uploadingSplitAttachment.value = true;
  try {
    await chat.uploadAttachment(target.files[0], splitChatId.value);
  } catch { alert('Ошибка при загрузке файла'); }
  finally { uploadingSplitAttachment.value = false; target.value = ''; }
};

const {
  isRecording: isSplitRecording,
  recordingDuration: splitRecordingDuration,
  toggleRecording: toggleSplitRecording,
  cancelRecording: cancelSplitRecording
} = useVoiceRecorder(async (file) => {
  if (!splitChatId.value) return;
  uploadingSplitAttachment.value = true;
  try {
    await chat.uploadAttachment(file, splitChatId.value);
  } finally { uploadingSplitAttachment.value = false; }
});

const splitChatObj = computed(() => chat.chats.find(c => c.id === splitChatId.value) || null);
const splitMessages = computed(() => splitChatId.value ? (chat.messages[splitChatId.value] || []) : []);

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
  contextMenu.value = { x: Math.min(clientX, window.innerWidth - 168), y: Math.min(clientY, window.innerHeight - 120), msg };
  reactionPicker.value = null;
};
const closeContextMenu = () => { contextMenu.value = null; reactionPicker.value = null; };
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

// ===== ВЫДЕЛЕНИЕ И УДАЛЕНИЕ =====
const selectedMessages = ref<Set<string>>(new Set());
const isSelecting = ref(false);

const toggleSelectMessage = (msgId: string) => {
  if (selectedMessages.value.has(msgId)) selectedMessages.value.delete(msgId);
  else selectedMessages.value.add(msgId);
  selectedMessages.value = new Set(selectedMessages.value);
};

const cancelSelection = () => {
  selectedMessages.value = new Set();
  isSelecting.value = false;
};

const deleteSelected = async () => {
  if (selectedMessages.value.size === 0) return;
  await chat.deleteMessages([...selectedMessages.value]);
  cancelSelection();
};

const onDeleteFromMenu = () => {
  if (!contextMenu.value) return;
  const msgId = contextMenu.value.msg.id;
  closeContextMenu();
  // Если уже выделяем — добавляем в выделение, иначе сразу удаляем
  if (isSelecting.value) {
    selectedMessages.value.add(msgId);
    selectedMessages.value = new Set(selectedMessages.value);
  } else {
    chat.deleteMessages([msgId]);
  }
};

const onSelectFromMenu = () => {
  if (!contextMenu.value) return;
  const msgId = contextMenu.value.msg.id;
  isSelecting.value = true;
  selectedMessages.value = new Set([msgId]);
  closeContextMenu();
};
const stickerPackModal = ref<null | { id: string, name: string, coverUrl?: string, stickers: any[], isAdded: boolean, createdBy: string }>(null);
const stickerPackModalLoading = ref(false);

const onStickerClick = async (packId: string) => {
  stickerPackModalLoading.value = true;
  stickerPackModal.value = null;
  try { stickerPackModal.value = await chat.getPack(packId); }
  finally { stickerPackModalLoading.value = false; }
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
const handleSendSticker = async (stickerId: string) => {
  await chat.sendSticker(stickerId);
  isStickerPickerOpen.value = false;
};
const handleToggleStickerPicker = async () => {
  isStickerPickerOpen.value = !isStickerPickerOpen.value;
  isChatEmojiPickerOpen.value = false;
  if (isStickerPickerOpen.value && !chat.stickerPacksLoaded) await chat.loadMyPacks();
};

// ===== ГОЛОСОВЫЕ =====
const { isRecording, recordingDuration, toggleRecording, cancelRecording } = useVoiceRecorder(async (file) => {
  uploadingAttachment.value = true;
  try {
    await chat.uploadAttachment(file);
    scrollToBottom();
  }
  finally { uploadingAttachment.value = false; }
});

// ===== ФАЙЛЫ =====
const triggerAttachmentInput = () => document.getElementById('attachmentInput')?.click();
const onAttachmentSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (!target.files || !target.files[0]) return;
  uploadingAttachment.value = true;
  try {
    await chat.uploadAttachment(target.files[0]);
    scrollToBottom();
  }
  catch { alert('Ошибка при загрузке файла'); }
  finally { uploadingAttachment.value = false; target.value = ''; }
};

// ===== КОНТАКТЫ =====
const isAddContactsOpen = ref(false);
const contactSearch = ref('');
const selectedUserIds = ref<Set<string>>(new Set());
const addingContacts = ref(false);

const filteredAllUsers = computed(() => {
  if (!contactSearch.value.trim()) return chat.allUsers;
  return chat.allUsers.filter(u => u.username.toLowerCase().includes(contactSearch.value.toLowerCase()));
});
const openAddContacts = async () => {
  isAddContactsOpen.value = true;
  selectedUserIds.value = new Set();
  contactSearch.value = '';
  await chat.loadAllUsers();
};
const toggleSelectUser = (userId: string) => {
  if (selectedUserIds.value.has(userId)) selectedUserIds.value.delete(userId);
  else selectedUserIds.value.add(userId);
  selectedUserIds.value = new Set(selectedUserIds.value);
};
const confirmAddContacts = async () => {
  if (selectedUserIds.value.size === 0) return;
  addingContacts.value = true;
  try {
    for (const userId of selectedUserIds.value) await chat.addContact(userId);
    isAddContactsOpen.value = false;
  } finally { addingContacts.value = false; }
};

// ===== ЭМОДЗИ ПРОФИЛЯ =====
const emojiCategories = [
  { label: '😀 Смайлы', emojis: ['😀','😁','😂','🤣','😃','😄','😅','😆','😉','😊','😋','😎','😍','🥰','😘','🙂','🤗','🤩','🤔','😐','😶','🙄','😏','😢','😭','😤','😠','😡','🤬','😷','🤒','🥱'] },
  { label: '👋 Жесты', emojis: ['👋','✋','👌','✌️','🤞','👍','👎','✊','👏','🙌','🙏','💪','🦾'] },
  { label: '❤️ Сердца', emojis: ['❤️','🧡','💛','💚','💙','💜','🖤','🤍','💔','💕','💞','💓','💗','💖','💘','💝'] },
  { label: '🌟 Символы', emojis: ['✨','⭐','🌟','💫','⚡','🔥','🌈','☄️','🌊','💥','🎉','🎊','🎁','🏆','🎯','🎲','🎨'] },
];
const filteredProfileEmojis = computed(() => {
  if (!emojiSearch.value.trim()) return emojiCategories;
  const all = emojiCategories.flatMap(c => c.emojis);
  return [{ label: '🔍 Результаты', emojis: all }];
});

// ===== СООБЩЕНИЯ =====
const currentMessages = computed(() =>
  chat.activeChat ? (chat.messages[chat.activeChat.id] || []) : []
);
const isOnline = (userId: string) => chat.onlineUsers.includes(userId);
const scrollToBottom = () => nextTick(() => messagesEnd.value?.scrollIntoView({ behavior: 'smooth' }));
const scrollToBottomSmooth = () => messagesEnd.value?.scrollIntoView({ behavior: 'smooth' });
const scrollToMessage = (msgId: string) => {
  const el = document.getElementById(`msg-${msgId}`);
  if (!el) return;
  el.scrollIntoView({ behavior: 'smooth', block: 'center' });
  el.classList.add('highlight-message');
  setTimeout(() => el.classList.remove('highlight-message'), 1500);
};
const onMessagesScroll = () => {
  if (!messagesContainer.value) return;
  const { scrollTop, scrollHeight, clientHeight } = messagesContainer.value;
  showScrollButton.value = scrollHeight - scrollTop - clientHeight > 150;
};

watch(currentMessages, (msgs, prevMsgs) => {
  // Скроллим вниз только если добавилось новое сообщение, не при обновлении реакций
  if (msgs.length > (prevMsgs?.length ?? 0)) {
    scrollToBottom();
  }
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
  // Не открываем чат автоматически — пусть пользователь выбирает сам

  // Закрытие чата по Escape
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && !isProfileOpen.value && !isAddContactsOpen.value && !stickerPackModal.value) {
      chat.activeChat = null;
      mobileView.value = 'list';
    }
  });
});
onUnmounted(() => chat.disconnect());

const openChat = async (c: any) => {
  messagesLoading.value = true;
  await chat.openChat(c);
  messagesLoading.value = false;
  mobileView.value = 'chat';
};
const goBackToList = () => { mobileView.value = 'list'; };
const sendMessage = async () => {
  if (!messageInput.value.trim()) return;
  await chat.sendMessage(messageInput.value);
  messageInput.value = '';
  scrollToBottom();
};
const handleKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMessage(); }
};
const handleLogout = () => { chat.disconnect(); auth.logout(); router.push('/'); };
const onSearch = async () => {
  if (searchQuery.value.trim()) { await chat.searchUsers(searchQuery.value); isSearchOpen.value = true; }
  else isSearchOpen.value = false;
};
const startDm = async (userId: string) => {
  searchQuery.value = ''; isSearchOpen.value = false;
  await chat.openDm(userId); mobileView.value = 'chat';
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
  if (newUsername.value.trim().length < 3) { usernameError.value = 'Ник должен быть не короче 3 символов'; return; }
  isSaving.value = true;
  try {
    if (newUsername.value.trim() !== auth.user?.username) await auth.changeUsername(newUsername.value.trim());
    await auth.updateProfile({ emojiPrefix: userPrefix.value, displayName: newUsername.value.trim() });
    isProfileOpen.value = false;
  } catch (e: any) {
    usernameError.value = e.response?.data || 'Не удалось сохранить настройки';
  } finally { isSaving.value = false; }
};
const triggerFileInput = () => document.getElementById('avatarInput')?.click();
const onFileSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files[0]) {
    try { await auth.uploadAvatar(target.files[0]); }
    catch { alert('Ошибка при загрузке фото'); }
  }
};
const formatTime = (dateStr: string) =>
  new Date(dateStr).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
const formatDuration = (sec: number) => {
  const m = Math.floor(sec / 60).toString().padStart(2, '0');
  const s = (sec % 60).toString().padStart(2, '0');
  return `${m}:${s}`;
};
const formatDateDivider = (dateStr: string) => {
  const d = new Date(dateStr);
  const today = new Date();
  const yesterday = new Date();
  yesterday.setDate(today.getDate() - 1);
  if (d.toDateString() === today.toDateString()) return 'Сегодня';
  if (d.toDateString() === yesterday.toDateString()) return 'Вчера';
  return d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });
};
const shouldShowDateDivider = (index: number) => {
  if (index === 0) return true;
  const prevMsg = currentMessages.value[index - 1];
  const currMsg = currentMessages.value[index];
  if (!prevMsg || !currMsg) return false;
  return new Date(prevMsg.sentAt).toDateString() !== new Date(currMsg.sentAt).toDateString();
};
const currentAvatar = computed(() => auth.user?.avatarUrl || '');
</script>

<template>
  <div class="w-full h-full min-h-screen max-w-[1920px] mx-auto p-2 sm:p-3 flex gap-2.5 text-slate-900 dark:text-slate-100 overflow-hidden font-sans select-none transition-colors duration-300 relative"
       :class="chatListPosition === 'left' ? 'flex-col md:flex-row' : 'flex-col'">
    <div class="ambient-canvas"></div>

    <!-- ===== ВЕРХНЯЯ / ЛЕВАЯ ЛЕНТА ЧАТОВ ===== -->
    <header :class="[
      'surface-strip rounded-2xl flex gap-2.5 z-20 shrink-0',
      chatListPosition === 'left'
        ? 'w-full md:w-64 md:h-full md:flex-col px-3 py-2 md:py-3 md:px-2.5 items-center md:items-stretch'
        : 'w-full px-3 py-2 flex-row items-center'
    ]">
      <!-- Логотип -->
      <div :class="[
        'flex items-center gap-2 shrink-0',
        chatListPosition === 'left'
          ? 'pr-2 border-r md:pr-0 md:border-r-0 md:pb-2 md:border-b border-slate-200 dark:border-white/[0.08] md:justify-between'
          : 'pr-2 border-r border-slate-200 dark:border-white/[0.08]'
      ]">
        <div class="flex items-center gap-2">
          <div class="w-8 h-8 text-[#3D7A5E] flex items-center justify-center">
            <svg viewBox="0 0 48 48" fill="none" class="w-7 h-7">
              <rect x="2" y="2" width="44" height="44" rx="12" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-opacity="0.22" stroke-width="1.5" />
              <ellipse cx="24" cy="24" rx="14" ry="6.5" transform="rotate(-40 24 24)" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
              <ellipse cx="24" cy="24" rx="14" ry="6.5" transform="rotate(40 24 24)" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-opacity="0.5" />
              <circle cx="24" cy="24" r="3.5" fill="currentColor" />
              <circle cx="34.5" cy="15.5" r="2" fill="currentColor" />
            </svg>
          </div>
          <span class="hidden sm:inline font-bold text-sm tracking-tight">Pozitron</span>
        </div>
      </div>

      <!-- Кнопка "Все диалоги" -->
      <button @click="isAllChatsOpen = true"
        :class="[
          'flex items-center gap-2 px-3 py-1.5 rounded-xl bg-black/[0.04] dark:bg-white/[0.05] hover:bg-black/[0.08] dark:hover:bg-white/[0.09] text-xs font-medium transition-colors shrink-0',
          chatListPosition === 'left' ? 'md:w-full md:justify-center' : ''
        ]">
        <LayoutGrid class="w-3.5 h-3.5 text-slate-500 dark:text-slate-400" />
        <span class="hidden sm:inline">Все диалоги</span>
      </button>

      <!-- Лента живых чатов -->
      <div :class="[
        'flex-1 flex gap-2 py-0.5',
        chatListPosition === 'left'
          ? 'items-center md:items-stretch overflow-x-auto md:overflow-x-hidden md:overflow-y-auto md:flex-col thin-scroll no-scrollbar'
          : 'items-center overflow-x-auto no-scrollbar'
      ]">
        <div v-for="c in [...chat.contactChats, ...chat.otherChats]" :key="c.id" @click="openChat(c)"
             :class="[
               'group relative flex items-center gap-2.5 px-2.5 py-1.5 rounded-xl cursor-pointer transition-all shrink-0 border',
               chatListPosition === 'left' ? 'max-w-[210px] md:max-w-none md:w-full' : 'max-w-[210px]',
               chat.activeChat?.id === c.id
                 ? 'bg-[#3D7A5E]/[0.12] border-[#3D7A5E]/30'
                 : 'border-transparent hover:bg-black/[0.03] dark:hover:bg-white/[0.04]'
             ]">
          <div class="relative shrink-0">
            <img v-if="c.avatarUrl && (c.isContact || c.type === 1)" :src="c.avatarUrl" class="w-8 h-8 rounded-full object-cover">
            <div v-else class="w-8 h-8 rounded-full bg-[#3D7A5E]/15 text-[#3D7A5E] flex items-center justify-center text-xs font-bold">
              <Globe v-if="c.type === 0 && !c.isContact" class="w-4 h-4" />
              <span v-else>{{ c.name?.[0]?.toUpperCase() || '?' }}</span>
            </div>
            <span v-if="c.unreadCount > 0" class="absolute -top-1 -right-1 min-w-4 h-4 px-1 bg-[#3D7A5E] text-white text-[10px] font-bold rounded-full flex items-center justify-center">
              {{ c.unreadCount > 9 ? '9+' : c.unreadCount }}
            </span>
          </div>
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-1">
              <p class="text-xs font-semibold truncate leading-tight">{{ c.name || 'Чат' }}</p>
              <BellOff v-if="isChatMuted(c.id)" class="w-3 h-3 text-slate-400 shrink-0" title="Уведомления отключены" />
            </div>
            <p class="text-[11px] text-slate-500 dark:text-slate-400 truncate leading-tight">{{ c.lastMessage || 'Нет сообщений' }}</p>
          </div>
          <!-- Кнопка Split View при наведении -->
          <button v-if="chat.activeChat && chat.activeChat.id !== c.id" @click.stop="openSplitChat(c)" title="Открыть в Split View"
            class="hidden md:flex opacity-0 group-hover:opacity-100 w-6 h-6 rounded-lg bg-white/80 dark:bg-black/40 hover:text-[#3D7A5E] items-center justify-center text-slate-400 transition-all shrink-0">
            <Columns2 class="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      <!-- Правые / нижние контролы -->
      <div :class="[
        'flex items-center gap-1.5 shrink-0',
        chatListPosition === 'left'
          ? 'pl-2 border-l md:pl-0 md:border-l-0 md:pt-2 md:border-t border-slate-200 dark:border-white/[0.08] md:justify-between'
          : 'pl-2 border-l border-slate-200 dark:border-white/[0.08]'
      ]">
        <button @click="toggleChatListPosition" :title="chatListPosition === 'top' ? 'Закрепить панель слева' : 'Закрепить панель сверху'"
          class="hidden md:flex w-8 h-8 rounded-xl hover:bg-black/[0.05] dark:hover:bg-white/[0.06] items-center justify-center text-slate-500 dark:text-slate-400 transition-colors">
          <PanelLeft v-if="chatListPosition === 'top'" class="w-4 h-4" />
          <PanelTop v-else class="w-4 h-4" />
        </button>
        <button @click="toggleTheme" :title="isDark ? 'Светлая тема' : 'Тёмная тема'"
          class="w-8 h-8 rounded-xl hover:bg-black/[0.05] dark:hover:bg-white/[0.06] flex items-center justify-center text-slate-500 dark:text-slate-400 transition-colors">
          <Sun v-if="isDark" class="w-4 h-4" />
          <Moon v-else class="w-4 h-4" />
        </button>
        <button v-if="auth.user?.role === 1" @click="router.push('/admin')" title="Админ панель"
          class="w-8 h-8 rounded-xl hover:bg-black/[0.05] dark:hover:bg-white/[0.06] flex items-center justify-center text-slate-500 dark:text-slate-400 transition-colors">
          <Shield class="w-4 h-4" />
        </button>
        <button @click="openProfile" title="Профиль" class="relative cursor-pointer ml-0.5">
          <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl" class="w-8 h-8 rounded-full object-cover border border-slate-300 dark:border-white/15">
          <div v-else class="w-8 h-8 rounded-full bg-[#3D7A5E] flex items-center justify-center text-xs font-bold text-white">{{ auth.user?.username?.[0]?.toUpperCase() }}</div>
          <span class="absolute bottom-0 right-0 w-2.5 h-2.5 rounded-full bg-emerald-500 ring-2 ring-white dark:ring-[#15171A]"></span>
        </button>
      </div>
    </header>

    <!-- МОДАЛКА "ВСЕ ДИАЛОГИ" -->
    <div v-if="isAllChatsOpen" @click="isAllChatsOpen = false" class="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
      <div @click.stop class="surface-sheet rounded-2xl w-full max-w-md max-h-[80vh] flex flex-col overflow-hidden">
        <div class="p-4 border-b border-slate-200 dark:border-white/[0.08] flex items-center justify-between">
          <h3 class="font-semibold text-sm">Все диалоги</h3>
          <div class="flex items-center gap-2">
            <button @click="openAddContacts(); isAllChatsOpen = false" class="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-[#3D7A5E]/15 text-[#3D7A5E] text-xs font-medium hover:bg-[#3D7A5E]/25 transition-colors">
              <UserPlus class="w-3.5 h-3.5" />
              <span>Контакты</span>
            </button>
            <button @click="handleLogout" title="Выйти" class="w-8 h-8 rounded-lg hover:bg-red-500/10 text-slate-400 hover:text-red-500 flex items-center justify-center transition-colors">
              <LogOut class="w-4 h-4" />
            </button>
            <button @click="isAllChatsOpen = false" class="w-8 h-8 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 flex items-center justify-center text-slate-400">
              <X class="w-4 h-4" />
            </button>
          </div>
        </div>
        <div class="p-3 border-b border-slate-200 dark:border-white/[0.08] relative">
          <input v-model="searchQuery" @input="onSearch" type="text" placeholder="Найти пользователя..."
                 class="w-full bg-black/[0.04] dark:bg-white/[0.04] border border-slate-200 dark:border-white/[0.08] rounded-xl px-3.5 py-2 text-sm outline-none focus:border-[#3D7A5E] select-text">
          <div v-if="isSearchOpen" class="mt-2 bg-white dark:bg-[#1C1E23] border border-slate-200 dark:border-white/[0.08] rounded-xl overflow-hidden max-h-40 overflow-y-auto">
            <div v-for="user in chat.users" :key="user.id" @click="startDm(user.id); isAllChatsOpen = false" class="flex items-center gap-3 px-3.5 py-2.5 hover:bg-black/5 dark:hover:bg-white/5 cursor-pointer">
              <div :class="['w-2 h-2 rounded-full shrink-0', isOnline(user.id) ? 'bg-emerald-500' : 'bg-slate-400']"></div>
              <span class="text-sm">{{ user.emojiPrefix || '' }} {{ user.username }}</span>
            </div>
            <div v-if="chat.users.length === 0" class="px-3.5 py-2 text-xs text-slate-400 text-center">Не найдено</div>
          </div>
        </div>
        <div class="flex-1 overflow-y-auto thin-scroll p-2 space-y-1">
          <div v-if="chat.contactChats.length > 0" class="px-3 pt-1 pb-1 text-[10px] font-bold uppercase tracking-wider text-slate-400">Личные</div>
          <div v-for="c in chat.contactChats" :key="c.id" @click="openChat(c); isAllChatsOpen = false"
               :class="['flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer transition-colors', chat.activeChat?.id === c.id ? 'bg-[#3D7A5E]/15 text-[#3D7A5E]' : 'hover:bg-black/[0.03] dark:hover:bg-white/[0.04]']">
            <div class="w-9 h-9 rounded-full bg-[#3D7A5E]/15 text-[#3D7A5E] flex items-center justify-center text-xs font-bold shrink-0">
              <span>{{ c.name?.[0]?.toUpperCase() || '?' }}</span>
            </div>
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium truncate">{{ c.name || 'Чат' }}</p>
              <p class="text-xs text-slate-400 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
            </div>
            <button v-if="chat.activeChat && chat.activeChat.id !== c.id" @click.stop="openSplitChat(c); isAllChatsOpen = false" title="Split View"
              class="hidden md:flex w-7 h-7 rounded-lg hover:bg-black/5 dark:hover:bg-white/10 items-center justify-center text-slate-400 hover:text-[#3D7A5E]">
              <Columns2 class="w-4 h-4" />
            </button>
          </div>
          <div v-if="chat.otherChats.length > 0" class="px-3 pt-2 pb-1 text-[10px] font-bold uppercase tracking-wider text-slate-400">Групповые</div>
          <div v-for="c in chat.otherChats" :key="c.id" @click="openChat(c); isAllChatsOpen = false"
               :class="['flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer transition-colors', chat.activeChat?.id === c.id ? 'bg-[#3D7A5E]/15 text-[#3D7A5E]' : 'hover:bg-black/[0.03] dark:hover:bg-white/[0.04]']">
            <div class="w-9 h-9 rounded-full bg-slate-200 dark:bg-white/10 flex items-center justify-center text-xs font-bold shrink-0">
              <Globe v-if="c.type === 0 && !c.isContact" class="w-4 h-4" />
              <span v-else>{{ c.name?.[0]?.toUpperCase() || '?' }}</span>
            </div>
            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium truncate">{{ c.name || 'Чат' }}</p>
              <p class="text-xs text-slate-400 truncate">{{ c.lastMessage || 'Нет сообщений' }}</p>
            </div>
            <button v-if="chat.activeChat && chat.activeChat.id !== c.id" @click.stop="openSplitChat(c); isAllChatsOpen = false" title="Split View"
              class="hidden md:flex w-7 h-7 rounded-lg hover:bg-black/5 dark:hover:bg-white/10 items-center justify-center text-slate-400 hover:text-[#3D7A5E]">
              <Columns2 class="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- ОСНОВНАЯ ОБЛАСТЬ (С ПОДДЕРЖКОЙ SPLIT VIEW) -->
    <div :class="['flex-1 flex min-w-0 min-h-0 gap-2.5 w-full relative z-10', splitDirection === 'horizontal' ? 'flex-col md:flex-row' : 'flex-col']">
    <main class="flex-1 flex flex-col surface-sheet rounded-2xl md:rounded-3xl relative overflow-hidden min-w-0 min-h-0">

      <header class="px-4 py-3 border-b border-slate-200/80 dark:border-white/[0.06] flex items-center justify-between z-10 shrink-0">
        <div class="flex items-center gap-2.5 min-w-0">
          <button @click="goBackToList" class="md:hidden w-9 h-9 flex items-center justify-center rounded-xl hover:bg-black/5 dark:hover:bg-white/5 transition-all text-[#3D7A5E]">
            <ChevronLeft class="w-6 h-6" />
          </button>
          <div v-if="chat.activeChat" class="flex items-center gap-2.5 min-w-0">
            <div class="w-9 h-9 rounded-full bg-[#3D7A5E]/15 text-[#3D7A5E] flex items-center justify-center text-sm font-bold shrink-0">
              <Globe v-if="chat.activeChat.type === 0 && !chat.activeChat.isContact" class="w-4 h-4" />
              <span v-else>{{ chat.activeChat.name?.[0]?.toUpperCase() || '?' }}</span>
            </div>
            <div class="min-w-0">
              <p class="font-bold text-sm truncate">{{ chat.activeChat.name || 'Чат' }}</p>
              <div class="flex items-center gap-1.5 text-[11px] text-slate-500 dark:text-slate-400">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                <span>в сети</span>
              </div>
            </div>
          </div>
          <div v-else class="flex items-center gap-2">
            <div class="w-2 h-2 rounded-full bg-slate-300 dark:bg-slate-600"></div>
            <span class="font-bold tracking-widest text-xs uppercase opacity-70">Выберите чат</span>
          </div>
        </div>
        <div v-if="chat.activeChat" class="flex items-center gap-1.5">
          <button @click="openAttachmentsModal(chat.activeChat.id, chat.activeChat.name || 'Чат', 'media')" title="Медиа" class="hidden sm:flex items-center gap-1 px-2.5 py-1 rounded-lg bg-black/[0.04] dark:bg-white/[0.05] hover:bg-black/[0.08] dark:hover:bg-white/[0.09] text-xs text-slate-600 dark:text-slate-300 transition-colors">
            <ImageIcon class="w-3.5 h-3.5" />
            <span>Медиа</span>
          </button>
          <button @click="openAttachmentsModal(chat.activeChat.id, chat.activeChat.name || 'Чат', 'files')" title="Файлы" class="hidden sm:flex items-center gap-1 px-2.5 py-1 rounded-lg bg-black/[0.04] dark:bg-white/[0.05] hover:bg-black/[0.08] dark:hover:bg-white/[0.09] text-xs text-slate-600 dark:text-slate-300 transition-colors">
            <FileText class="w-3.5 h-3.5" />
            <span>Файлы</span>
          </button>
          <button @click="chat.activeChat = null; mobileView = 'list'" title="Закрыть чат"
            class="w-8 h-8 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 flex items-center justify-center text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors">
            <X class="w-4 h-4" />
          </button>
        </div>
      </header>

      <!-- Лента сообщений -->
      <div ref="messagesContainer" @scroll="onMessagesScroll" class="flex-1 px-3 sm:px-5 py-4 overflow-y-auto thin-scroll z-10 space-y-3 select-text relative">
        <div v-if="messagesLoading" class="flex h-full items-center justify-center">
          <div class="flex flex-col items-center gap-3 opacity-50">
            <div class="w-8 h-8 border-2 border-[#3D7A5E] border-t-transparent rounded-full animate-spin"></div>
            <p class="text-xs text-slate-400 dark:text-slate-500">Загрузка сообщений...</p>
          </div>
        </div>
        <div v-else-if="!chat.activeChat" class="flex h-full items-center justify-center opacity-20">
          <div class="text-center"><MessageSquare class="w-10 h-10 mx-auto mb-2 stroke-[1.5]" /><p class="text-sm italic font-medium">Выберите чат</p></div>
        </div>

        <template v-else>
          <div v-if="currentMessages.length === 0" class="flex justify-center pt-10 opacity-30">
            <p class="text-sm italic">Сообщений пока нет. Будь первым!</p>
          </div>

          <template v-for="(msg, index) in currentMessages" :key="msg.id">
            <!-- Разделитель дней -->
            <div v-if="shouldShowDateDivider(index)" class="flex justify-center my-3">
              <span class="px-3 py-1 rounded-full text-[11px] font-medium bg-black/[0.05] dark:bg-white/[0.06] text-slate-500 dark:text-slate-400">
                {{ formatDateDivider(msg.sentAt) }}
              </span>
            </div>

            <div :id="`msg-${msg.id}`"
                 :class="['flex gap-2 items-end transition-colors rounded-xl px-1',
                   msg.userId === auth.user?.id ? 'flex-row-reverse' : '',
                   isSelecting && selectedMessages.has(msg.id) ? 'bg-[#3D7A5E]/15' : '']"
                 @click="isSelecting && msg.userId === auth.user?.id ? toggleSelectMessage(msg.id) : null">

              <!-- Чекбокс выделения (только свои) -->
              <div v-if="isSelecting && msg.userId === auth.user?.id"
                   :class="['shrink-0 self-center w-5 h-5 rounded-full border-2 flex items-center justify-center transition-all cursor-pointer',
                     selectedMessages.has(msg.id) ? 'bg-[#3D7A5E] border-[#3D7A5E]' : 'border-slate-300 dark:border-slate-600']"
                   :style="msg.userId === auth.user?.id ? 'order: -1' : ''">
                <svg v-if="selectedMessages.has(msg.id)" viewBox="0 0 16 16" class="w-3 h-3 fill-white">
                  <path d="M13.5 3.5L6 11L2.5 7.5L1.5 8.5L6 13L14.5 4.5L13.5 3.5Z"/>
                </svg>
              </div>
              <img v-if="msg.avatarUrl" :src="msg.avatarUrl" class="w-7 h-7 rounded-lg object-cover shrink-0 mb-5">
              <div v-else class="w-7 h-7 rounded-lg bg-[#3D7A5E] flex items-center justify-center text-xs font-bold shrink-0 mb-5 text-white">{{ msg.username?.[0]?.toUpperCase() }}</div>

              <div :class="['flex flex-col gap-0.5 max-w-xs sm:max-w-sm', msg.userId === auth.user?.id ? 'items-end' : 'items-start']">
                <span class="text-[10px] text-slate-400 dark:text-slate-500 px-1">{{ msg.emojiPrefix }} {{ msg.username }} · {{ formatTime(msg.sentAt) }}</span>
                <div @contextmenu.prevent="showContextMenu($event, msg)"
                     @touchstart="onMessageLongPress($event, msg)"
                     @touchend="onMessageLongPressCancel"
                     @touchmove="onMessageLongPressCancel">
                  <MessageBubble
                    :msg="msg"
                    :is-own="msg.userId === auth.user?.id"
                    :on-sticker-click="onStickerClick"
                    :on-reply="chat.setReply"
                    :on-react="(id) => { reactionPicker = { x: 0, y: 0, msgId: id }; }"
                    :scroll-to-message="scrollToMessage"
                    :add-reaction="chat.addReaction"
                    :current-user-id="auth.user?.id ?? ''"
                  />
                </div>
              </div>
            </div>
          </template>
        </template>

        <div ref="messagesEnd"></div>
      </div>

      <!-- Кнопка вниз -->
      <Transition name="fade">
        <button v-if="showScrollButton" @click="scrollToBottomSmooth"
                class="absolute bottom-24 right-5 z-20 w-10 h-10 rounded-full bg-[#3D7A5E] hover:bg-[#346950] text-white shadow-lg flex items-center justify-center transition-all active:scale-90">
          <ArrowDown class="w-5 h-5" />
        </button>
      </Transition>

      <!-- Панель удаления при выделении -->
      <Transition name="picker">
        <div v-if="isSelecting"
             class="px-4 py-3 bg-white dark:bg-slate-900 border-t border-slate-200 dark:border-slate-800 flex items-center gap-3 z-20">
          <button @click="cancelSelection" class="w-9 h-9 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center justify-center transition-all active:scale-90">
            <svg viewBox="0 0 24 24" class="w-5 h-5" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M18 6L6 18M6 6l12 12" stroke-linecap="round"/></svg>
          </button>
          <span class="flex-1 text-sm font-bold text-slate-700 dark:text-slate-300">
            {{ selectedMessages.size > 0 ? `Выбрано: ${selectedMessages.size}` : 'Выберите сообщения' }}
          </span>
          <button @click="deleteSelected" :disabled="selectedMessages.size === 0"
                  class="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-500 hover:bg-red-600 text-white text-sm font-bold transition-all active:scale-95 disabled:opacity-40">
            <svg viewBox="0 0 24 24" class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
            Удалить
          </button>
        </div>
      </Transition>

      <!-- Остров ввода сообщений -->
      <footer class="px-3 pb-3 pt-1 z-10 shrink-0 relative">
        <div class="max-w-3xl mx-auto relative">
          <Transition name="picker">
            <StickerPicker v-if="isStickerPickerOpen" @send-sticker="handleSendSticker" @close="isStickerPickerOpen = false" />
          </Transition>
          <Transition name="picker">
            <EmojiPicker v-if="isChatEmojiPickerOpen" class="mb-2" @select="(e) => { messageInput += e; isChatEmojiPickerOpen = false; }" />
          </Transition>

          <!-- Превью ответа -->
          <Transition name="picker">
            <div v-if="chat.replyingTo" class="mb-2">
              <div class="flex items-center gap-2 surface-composer rounded-xl px-3 py-2 shadow-lg">
                <div class="w-0.5 h-8 bg-[#3D7A5E] rounded-full shrink-0"></div>
                <div class="flex-1 min-w-0">
                  <p class="text-xs font-bold text-[#3D7A5E]">{{ chat.replyingTo.username }}</p>
                  <p class="text-xs text-slate-500 dark:text-slate-400 truncate flex items-center gap-1">
                    <template v-if="chat.replyingTo.type === 'Voice'"><Mic class="w-3.5 h-3.5 shrink-0" /><span>Голосовое</span></template>
                    <template v-else-if="chat.replyingTo.type === 'Image'"><ImageIcon class="w-3.5 h-3.5 shrink-0" /><span>Изображение</span></template>
                    <template v-else-if="chat.replyingTo.type === 'Video'"><Video class="w-3.5 h-3.5 shrink-0" /><span>Видео</span></template>
                    <template v-else-if="chat.replyingTo.type === 'Sticker'"><Sticker class="w-3.5 h-3.5 shrink-0" /><span>Стикер</span></template>
                    <template v-else>{{ chat.replyingTo.content }}</template>
                  </p>
                </div>
                <button @click="chat.setReply(null)" class="shrink-0 w-6 h-6 flex items-center justify-center rounded-lg hover:bg-black/5 dark:hover:bg-white/5 text-slate-400 transition-all">
                  <X class="w-4 h-4" />
                </button>
              </div>
            </div>
          </Transition>

          <!-- Режим записи голосового -->
          <div v-if="isRecording" class="surface-composer rounded-2xl p-2 flex items-center gap-3">
            <button @click="cancelRecording" title="Отменить запись"
                    class="p-2 rounded-xl text-red-500 hover:bg-red-500/10 transition-colors shrink-0">
              <Trash2 class="w-5 h-5" />
            </button>
            <div class="flex items-center gap-2 flex-1 min-w-0">
              <span class="w-2.5 h-2.5 rounded-full bg-red-500 animate-pulse shrink-0"></span>
              <span class="text-xs font-mono font-semibold tabular-nums text-slate-700 dark:text-slate-200">{{ formatDuration(recordingDuration) }}</span>
              <div class="flex items-center gap-1 flex-1 h-6 overflow-hidden px-2">
                <span v-for="i in 16" :key="i" class="rec-wave-bar w-1 bg-[#3D7A5E] rounded-full" :style="{ animationDelay: `${(i % 5) * 0.12}s` }"></span>
              </div>
            </div>
            <button @click="toggleRecording" title="Отправить голосовое"
                    class="bg-[#3D7A5E] hover:bg-[#346950] text-white p-2.5 rounded-xl transition-colors shrink-0">
              <Send class="w-5 h-5" />
            </button>
          </div>

          <!-- Обычный режим ввода -->
          <div v-else class="surface-composer flex items-end gap-1.5 p-1.5 rounded-2xl transition-all">
            <button @click="triggerAttachmentInput" :disabled="!chat.activeChat || uploadingAttachment"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5', !chat.activeChat ? 'opacity-40 cursor-not-allowed' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400']">
              <div v-if="uploadingAttachment" class="w-5 h-5 flex items-center justify-center">
                <div class="w-4 h-4 border-2 border-[#3D7A5E] border-t-transparent rounded-full animate-spin"></div>
              </div>
              <Paperclip v-else class="w-5 h-5 text-[#3D7A5E]" />
            </button>
            <input id="attachmentInput" type="file" class="hidden" accept="image/png,image/jpeg,image/gif,image/webp,video/mp4,video/webm" @change="onAttachmentSelected">

            <button @click.stop="handleToggleStickerPicker" :disabled="!chat.activeChat"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                      isStickerPickerOpen ? 'bg-[#3D7A5E]/20 text-[#3D7A5E]' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400',
                      !chat.activeChat ? 'opacity-40 cursor-not-allowed' : '']">
              <Sticker class="w-5 h-5" />
            </button>

            <button @click.stop="isChatEmojiPickerOpen = !isChatEmojiPickerOpen; isStickerPickerOpen = false"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                      isChatEmojiPickerOpen ? 'bg-[#3D7A5E]/20 text-[#3D7A5E]' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400']">
              <Smile class="w-5 h-5" />
            </button>

            <button @click="toggleChatMute(chat.activeChat?.id)" :disabled="!chat.activeChat"
                    :title="isChatMuted(chat.activeChat?.id) ? 'Включить уведомления' : 'Отключить уведомления'"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                      isChatMuted(chat.activeChat?.id) ? 'bg-[#3D7A5E]/20 text-[#3D7A5E]' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400',
                      !chat.activeChat ? 'opacity-40 cursor-not-allowed' : '']">
              <BellOff v-if="isChatMuted(chat.activeChat?.id)" class="w-5 h-5" />
              <Bell v-else class="w-5 h-5" />
            </button>

            <textarea v-model="messageInput" @keydown="handleKeydown"
                      :placeholder="chat.activeChat ? 'Напиши что-нибудь...' : 'Выберите чат...'"
                      :disabled="!chat.activeChat" rows="1"
                      class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-400 dark:placeholder:text-slate-500 select-text disabled:opacity-40 resize-none max-h-32 overflow-y-auto thin-scroll text-slate-900 dark:text-slate-100"
                      style="field-sizing: content;"></textarea>

            <button v-if="messageInput.trim()" @click="sendMessage" :disabled="!chat.activeChat"
                    class="bg-[#3D7A5E] hover:bg-[#346950] text-white p-2.5 rounded-xl transition-colors shrink-0 self-end">
              <Send class="w-5 h-5" />
            </button>

            <button v-else-if="chat.activeChat" @click="toggleRecording"
                    class="bg-[#3D7A5E] hover:bg-[#346950] p-2.5 rounded-xl text-white transition-colors shrink-0 self-end">
              <Mic class="w-5 h-5" />
            </button>
          </div>
        </div>
      </footer>
    </main>

    <!-- ВТОРОЙ ЧАТ (SPLIT VIEW) -->
    <section v-if="splitChatObj" class="hidden md:flex flex-1 flex-col surface-sheet rounded-2xl relative overflow-hidden min-w-0 min-h-0">
      <header class="px-4 py-3 border-b border-slate-200/80 dark:border-white/[0.06] flex items-center justify-between shrink-0">
        <div class="flex items-center gap-2 min-w-0">
          <div class="w-8 h-8 rounded-lg bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-sm shrink-0">
            <Globe v-if="splitChatObj.type === 0" class="w-4 h-4 text-slate-500 dark:text-slate-300" />
            <span v-else>{{ splitChatObj.name?.[0]?.toUpperCase() }}</span>
          </div>
          <p class="font-bold text-sm truncate">{{ splitChatObj.name || 'Чат' }}</p>
        </div>
        <div class="flex items-center gap-1.5">
          <button @click="openAttachmentsModal(splitChatObj.id, splitChatObj.name || 'Чат', 'media')" title="Медиа"
            class="hidden sm:flex items-center gap-1 px-2 py-1 rounded-lg bg-black/[0.04] dark:bg-white/[0.05] hover:bg-black/[0.08] dark:hover:bg-white/[0.09] text-xs text-slate-600 dark:text-slate-300 transition-colors">
            <ImageIcon class="w-3.5 h-3.5" />
          </button>
          <button @click="openAttachmentsModal(splitChatObj.id, splitChatObj.name || 'Чат', 'files')" title="Файлы"
            class="hidden sm:flex items-center gap-1 px-2 py-1 rounded-lg bg-black/[0.04] dark:bg-white/[0.05] hover:bg-black/[0.08] dark:hover:bg-white/[0.09] text-xs text-slate-600 dark:text-slate-300 transition-colors">
            <FileText class="w-3.5 h-3.5" />
          </button>
          <button @click="toggleSplitDirection" :title="splitDirection === 'horizontal' ? 'Расположить друг над другом' : 'Расположить рядом'"
            class="w-8 h-8 rounded-lg hover:bg-slate-100 dark:hover:bg-white/[0.06] flex items-center justify-center text-slate-500 dark:text-slate-400 transition-colors">
            <Rows2 v-if="splitDirection === 'horizontal'" class="w-4 h-4" />
            <Columns2 v-else class="w-4 h-4" />
          </button>
          <button @click="closeSplitChat" title="Закрыть второй чат"
            class="w-8 h-8 rounded-lg hover:bg-red-500/10 flex items-center justify-center text-slate-400 hover:text-red-500 transition-colors">
            <X class="w-4 h-4" />
          </button>
        </div>
      </header>
      <div class="flex-1 px-3 sm:px-5 py-4 overflow-y-auto thin-scroll space-y-3 select-text">
        <div v-if="splitMessages.length === 0" class="flex justify-center pt-10 opacity-30">
          <p class="text-sm italic">Сообщений пока нет</p>
        </div>
        <div v-for="msg in splitMessages" :key="msg.id"
             :class="['flex gap-2 items-end transition-colors rounded-xl px-1', msg.userId === auth.user?.id ? 'flex-row-reverse' : '']">
          <img v-if="msg.avatarUrl" :src="msg.avatarUrl" class="w-7 h-7 rounded-lg object-cover shrink-0 mb-5">
          <div v-else class="w-7 h-7 rounded-lg bg-[#3D7A5E] flex items-center justify-center text-xs font-bold shrink-0 mb-5 text-white">{{ msg.username?.[0]?.toUpperCase() }}</div>
          <div :class="['flex flex-col gap-0.5 max-w-xs sm:max-w-sm', msg.userId === auth.user?.id ? 'items-end' : 'items-start']">
            <span class="text-[10px] text-slate-400 dark:text-slate-500 px-1">{{ msg.emojiPrefix }} {{ msg.username }} · {{ formatTime(msg.sentAt) }}</span>
            <MessageBubble
              :msg="msg"
              :is-own="msg.userId === auth.user?.id"
              :on-sticker-click="onStickerClick"
              :on-reply="(m: any) => chat.setReply(m)"
              :on-react="() => {}"
              :scroll-to-message="() => {}"
              :add-reaction="(id: string, emoji: string) => chat.addReaction(id, emoji)"
              :current-user-id="auth.user?.id || ''"
            />
          </div>
        </div>
      </div>
      <!-- Полноценный остров ввода второго чата -->
      <footer class="px-3 pb-3 pt-1 z-10 shrink-0 relative">
        <div class="relative">
          <Transition name="picker">
            <StickerPicker v-if="isSplitStickerPickerOpen" @send-sticker="handleSendSplitSticker" @close="isSplitStickerPickerOpen = false" />
          </Transition>
          <Transition name="picker">
            <EmojiPicker v-if="isSplitEmojiPickerOpen" class="mb-2" @select="(e) => { splitMessageInput += e; isSplitEmojiPickerOpen = false; }" />
          </Transition>

          <!-- Режим записи голосового во втором чате -->
          <div v-if="isSplitRecording" class="surface-composer rounded-2xl p-2 flex items-center gap-3">
            <button @click="cancelSplitRecording" title="Отменить запись"
                    class="p-2 rounded-xl text-red-500 hover:bg-red-500/10 transition-colors shrink-0">
              <Trash2 class="w-5 h-5" />
            </button>
            <div class="flex items-center gap-2 flex-1 min-w-0">
              <span class="w-2.5 h-2.5 rounded-full bg-red-500 animate-pulse shrink-0"></span>
              <span class="text-xs font-mono font-semibold tabular-nums text-slate-700 dark:text-slate-200">{{ formatDuration(splitRecordingDuration) }}</span>
              <div class="flex items-center gap-1 flex-1 h-6 overflow-hidden px-2">
                <span v-for="i in 12" :key="i" class="rec-wave-bar w-1 bg-[#3D7A5E] rounded-full" :style="{ animationDelay: `${(i % 5) * 0.12}s` }"></span>
              </div>
            </div>
            <button @click="toggleSplitRecording" title="Отправить голосовое"
                    class="bg-[#3D7A5E] hover:bg-[#346950] text-white p-2.5 rounded-xl transition-colors shrink-0">
              <Send class="w-5 h-5" />
            </button>
          </div>

          <!-- Обычный режим ввода во втором чате -->
          <div v-else class="surface-composer flex items-end gap-1.5 p-1.5 rounded-2xl transition-all">
            <button @click="triggerSplitAttachmentInput" :disabled="uploadingSplitAttachment"
                    class="p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5 hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400">
              <div v-if="uploadingSplitAttachment" class="w-5 h-5 flex items-center justify-center">
                <div class="w-4 h-4 border-2 border-[#3D7A5E] border-t-transparent rounded-full animate-spin"></div>
              </div>
              <Paperclip v-else class="w-5 h-5 text-[#3D7A5E]" />
            </button>
            <input id="splitAttachmentInput" type="file" class="hidden" accept="image/png,image/jpeg,image/gif,image/webp,video/mp4,video/webm" @change="onSplitAttachmentSelected">

            <button @click.stop="handleToggleSplitStickerPicker"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                      isSplitStickerPickerOpen ? 'bg-[#3D7A5E]/20 text-[#3D7A5E]' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400']">
              <Sticker class="w-5 h-5" />
            </button>

            <button @click.stop="isSplitEmojiPickerOpen = !isSplitEmojiPickerOpen; isSplitStickerPickerOpen = false"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                      isSplitEmojiPickerOpen ? 'bg-[#3D7A5E]/20 text-[#3D7A5E]' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400']">
              <Smile class="w-5 h-5" />
            </button>

            <button @click="toggleChatMute(splitChatId)" :title="isChatMuted(splitChatId) ? 'Включить уведомления' : 'Отключить уведомления'"
                    :class="['p-2 rounded-xl transition-colors shrink-0 self-end mb-0.5',
                      isChatMuted(splitChatId) ? 'bg-[#3D7A5E]/20 text-[#3D7A5E]' : 'hover:bg-black/5 dark:hover:bg-white/5 text-slate-500 dark:text-slate-400']">
              <BellOff v-if="isChatMuted(splitChatId)" class="w-5 h-5" />
              <Bell v-else class="w-5 h-5" />
            </button>

            <textarea v-model="splitMessageInput" @keydown="handleSplitKeydown"
                      placeholder="Написать во второй чат..." rows="1"
                      class="flex-1 bg-transparent border-none px-2 py-2 outline-none text-sm placeholder:text-slate-400 dark:placeholder:text-slate-500 select-text resize-none max-h-32 overflow-y-auto thin-scroll text-slate-900 dark:text-slate-100"
                      style="field-sizing: content;"></textarea>

            <button v-if="splitMessageInput.trim()" @click="sendSplitMessage"
                    class="bg-[#3D7A5E] hover:bg-[#346950] text-white p-2.5 rounded-xl transition-colors shrink-0 self-end">
              <Send class="w-5 h-5" />
            </button>

            <button v-else @click="toggleSplitRecording"
                    class="bg-[#3D7A5E] hover:bg-[#346950] p-2.5 rounded-xl text-white transition-colors shrink-0 self-end">
              <Mic class="w-5 h-5" />
            </button>
          </div>
        </div>
      </footer>
    </section>
    </div>

    <!-- МОДАЛКА КОНТАКТОВ -->
    <Transition name="fade">
      <div v-if="isAddContactsOpen" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4">
        <div @click="isAddContactsOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div class="p-5 border-b border-slate-200 dark:border-slate-800">
            <p class="font-black text-lg">Добавить контакты</p>
            <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">Выбери пользователей</p>
          </div>
          <div class="px-4 pt-3">
            <input v-model="contactSearch" type="text" placeholder="Поиск..." class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-4 py-2.5 text-sm outline-none focus:ring-1 focus:ring-blue-500 select-text text-slate-900 dark:text-slate-100">
          </div>
          <div class="max-h-64 overflow-y-auto p-2 mt-2">
            <div v-if="chat.allUsers.length === 0" class="flex items-center justify-center py-8 opacity-30"><p class="text-sm">Загрузка...</p></div>
            <div v-for="user in filteredAllUsers" :key="user.id" @click="toggleSelectUser(user.id)"
                 class="flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer transition-all hover:bg-slate-100 dark:hover:bg-slate-800 active:bg-slate-200 dark:active:bg-slate-700">
              <img v-if="user.avatarUrl" :src="user.avatarUrl" class="w-10 h-10 rounded-xl object-cover shrink-0">
              <div v-else class="w-10 h-10 rounded-xl bg-blue-600 flex items-center justify-center text-sm font-bold shrink-0 text-white">{{ user.username?.[0]?.toUpperCase() }}</div>
              <div class="flex-1 min-w-0">
                <p class="text-sm font-bold truncate">{{ user.emojiPrefix || '' }} {{ user.username }}</p>
                <div class="flex items-center gap-1 mt-0.5">
                  <div :class="['w-1.5 h-1.5 rounded-full', isOnline(user.id) ? 'bg-green-500' : 'bg-slate-300 dark:bg-slate-600']"></div>
                  <p class="text-xs text-slate-400 dark:text-slate-500">{{ isOnline(user.id) ? 'онлайн' : 'оффлайн' }}</p>
                </div>
              </div>
              <div :class="['w-6 h-6 rounded-lg border-2 flex items-center justify-center transition-all shrink-0', selectedUserIds.has(user.id) ? 'bg-[#3D7A5E] border-[#3D7A5E]' : 'border-slate-300 dark:border-slate-600']">
                <svg v-if="selectedUserIds.has(user.id)" viewBox="0 0 16 16" class="w-3.5 h-3.5 fill-white"><path d="M13.5 3.5L6 11L2.5 7.5L1.5 8.5L6 13L14.5 4.5L13.5 3.5Z"/></svg>
              </div>
            </div>
          </div>
          <div class="p-4 border-t border-slate-200 dark:border-slate-800 flex gap-3">
            <button @click="isAddContactsOpen = false" class="flex-1 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-900 dark:text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">Отмена</button>
            <button @click="confirmAddContacts" :disabled="selectedUserIds.size === 0 || addingContacts"
                    class="flex-1 bg-[#3D7A5E] hover:bg-[#346950] text-white font-bold py-3 rounded-xl text-sm shadow-lg active:scale-95 transition-all disabled:opacity-50">
              {{ addingContacts ? 'Добавляем...' : `Добавить${selectedUserIds.size > 0 ? ` (${selectedUserIds.size})` : ''}` }}
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- МОДАЛКА ПАКА -->
    <Transition name="fade">
      <div v-if="stickerPackModal || stickerPackModalLoading" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4">
        <div @click="stickerPackModal = null; stickerPackModalLoading = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div v-if="stickerPackModalLoading" class="flex items-center justify-center py-16">
            <div class="w-8 h-8 border-2 border-blue-500 border-t-transparent rounded-full animate-spin"></div>
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
                      :class="['w-full font-bold py-3 rounded-xl text-sm active:scale-95 transition-all', stickerPackModal.isAdded ? 'bg-red-500/10 border border-red-500/20 text-red-500 dark:text-red-400 hover:bg-red-500/20' : 'bg-gradient-to-r from-blue-600 to-indigo-600 text-white shadow-lg']">
                {{ stickerPackModal.isAdded ? '— Убрать пак' : '+ Добавить пак' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Transition>

    <!-- МОДАЛКА ПРОФИЛЯ -->
    <Transition name="fade">
      <div v-if="isProfileOpen" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
        <div @click="isProfileOpen = false; isEmojiPickerOpen = false" class="absolute inset-0 bg-slate-950/80 backdrop-blur-sm"></div>
        <div class="relative bg-white dark:bg-slate-900 border border-slate-200 dark:border-white/10 w-full sm:max-w-sm rounded-t-3xl sm:rounded-3xl overflow-hidden shadow-2xl">
          <div class="h-20 bg-gradient-to-r from-[#3D7A5E] to-[#2c5944]"></div>
          <div class="px-6 pb-8">
            <div class="relative -mt-10 mb-4 flex justify-center">
              <div @click="triggerFileInput" class="relative group cursor-pointer">
                <img v-if="currentAvatar" :src="currentAvatar" class="w-20 h-20 rounded-2xl border-4 border-white dark:border-slate-900 object-cover shadow-xl">
                <div v-else class="w-20 h-20 rounded-2xl border-4 border-white dark:border-slate-900 bg-slate-200 dark:bg-slate-800 flex items-center justify-center text-2xl font-bold uppercase">{{ auth.user?.username?.[0] }}</div>
                <div class="absolute inset-0 bg-black/40 rounded-2xl opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                  <span class="text-[10px] font-bold uppercase text-white">Изменить</span>
                </div>
                <input id="avatarInput" type="file" class="hidden" accept="image/*" @change="onFileSelected">
              </div>
            </div>
            <div class="text-center mb-5"><h3 class="text-xl font-black">{{ auth.user?.username }}</h3></div>
            <div class="space-y-4">
              <div>
                <label class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase ml-2 mb-1 block">Никнейм</label>
                <input v-model="newUsername" type="text" placeholder="username" maxlength="32" @input="usernameError = ''"
                       :class="['w-full bg-slate-100 dark:bg-slate-800/50 border rounded-xl px-4 py-3 text-sm outline-none focus:ring-1 transition-all select-text text-slate-900 dark:text-slate-100', usernameError ? 'border-red-500/70 focus:ring-red-500' : 'border-slate-200 dark:border-slate-700 focus:ring-blue-500']">
                <p v-if="usernameError" class="text-[10px] text-red-400 ml-2 mt-1">{{ usernameError }}</p>
              </div>
              <div>
                <label class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase ml-2 mb-1 block">Префикс (эмодзи)</label>
                <div class="flex gap-2">
                  <input v-model="userPrefix" type="text" placeholder="🚀" maxlength="2" class="flex-1 bg-slate-100 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-xl px-4 py-3 text-center text-2xl outline-none focus:ring-1 focus:ring-blue-500 select-text">
                  <button @click.stop="isEmojiPickerOpen = !isEmojiPickerOpen"
                          :class="['px-4 rounded-xl border flex items-center justify-center transition-all text-slate-500 dark:text-slate-300', isEmojiPickerOpen ? 'bg-blue-600/20 border-blue-500 text-blue-600 dark:text-blue-400' : 'bg-slate-100 dark:bg-slate-800/50 border-slate-200 dark:border-slate-700']">
                    <Smile class="w-5 h-5" />
                  </button>
                </div>
                <Transition name="picker">
                  <div v-if="isEmojiPickerOpen" class="mt-2 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden" @click.stop>
                    <div class="p-2 border-b border-slate-200 dark:border-slate-800">
                      <input v-model="emojiSearch" type="text" placeholder="Поиск..." class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text text-slate-900 dark:text-slate-100">
                    </div>
                    <div class="max-h-44 overflow-y-auto p-2 space-y-3">
                      <div v-for="cat in filteredProfileEmojis" :key="cat.label">
                        <p class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
                        <div class="flex flex-wrap gap-0.5">
                          <button v-for="emoji in cat.emojis" :key="emoji" @click="userPrefix = emoji; isEmojiPickerOpen = false"
                                  :class="['w-9 h-9 flex items-center justify-center rounded-lg text-xl transition-all active:scale-90', userPrefix === emoji ? 'bg-blue-600/40 ring-1 ring-blue-500' : 'hover:bg-slate-100 dark:hover:bg-slate-700']">
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
              <button @click="isProfileOpen = false; isEmojiPickerOpen = false" class="flex-1 bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-900 dark:text-white font-bold py-3 rounded-xl text-sm transition-all active:scale-95">Отмена</button>
              <button @click="saveProfile" :disabled="isSaving" class="flex-1 bg-[#3D7A5E] hover:bg-[#346950] text-white font-bold py-3 rounded-xl text-sm shadow-lg active:scale-95 transition-all disabled:opacity-50">{{ isSaving ? 'Ждем...' : 'Сохранить' }}</button>
            </div>
            <button @click="handleLogout" class="mt-3 w-full flex items-center justify-center gap-2 py-2.5 rounded-xl bg-red-500/10 hover:bg-red-500/20 text-red-500 text-xs font-bold transition-colors">
              <LogOut class="w-4 h-4" />
              <span>Выйти из аккаунта</span>
            </button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- КОНТЕКСТНОЕ МЕНЮ -->
    <Transition name="fade">
      <div v-if="contextMenu" class="fixed inset-0 z-50" @click="closeContextMenu" @contextmenu.prevent="closeContextMenu">
        <div :style="{ position: 'fixed', left: contextMenu.x + 'px', top: contextMenu.y + 'px' }"
             class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-2xl shadow-2xl overflow-hidden w-44" @click.stop>
          <button @click="onReplyFromMenu" class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all active:bg-slate-200 dark:active:bg-slate-700">
            <svg viewBox="0 0 24 24" class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 17l-5-5 5-5M20 18v-2a4 4 0 0 0-4-4H4" stroke-linecap="round" stroke-linejoin="round"/></svg>
            Ответить
          </button>
          <button @click="onReactFromMenu" class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all active:bg-slate-200 dark:active:bg-slate-700 border-t border-slate-100 dark:border-slate-800">
            <svg viewBox="0 0 24 24" class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><circle cx="9" cy="10" r="1" fill="currentColor" stroke="none"/><circle cx="15" cy="10" r="1" fill="currentColor" stroke="none"/><path d="M8 14c1 2 7 2 8 0" stroke-linecap="round"/></svg>
            Реакция
          </button>
          <!-- Только для своих сообщений -->
          <template v-if="contextMenu.msg.userId === auth.user?.id">
            <button @click="onSelectFromMenu" class="w-full flex items-center gap-3 px-4 py-3 text-sm text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-all active:bg-slate-200 dark:active:bg-slate-700 border-t border-slate-100 dark:border-slate-800">
              <svg viewBox="0 0 24 24" class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" stroke-linecap="round" stroke-linejoin="round"/></svg>
              Выбрать
            </button>
            <button @click="onDeleteFromMenu" class="w-full flex items-center gap-3 px-4 py-3 text-sm text-red-500 hover:bg-red-50 dark:hover:bg-red-500/10 transition-all active:bg-red-100 dark:active:bg-red-500/20 border-t border-slate-100 dark:border-slate-800">
              <svg viewBox="0 0 24 24" class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
              Удалить
            </button>
          </template>
        </div>
      </div>
    </Transition>

    <!-- ПИКЕР РЕАКЦИЙ -->
    <Transition name="fade">
      <div v-if="reactionPicker" class="fixed inset-0 z-50" @click="closeContextMenu">
        <div :style="{ position: 'fixed', left: reactionPicker.x + 'px', top: reactionPicker.y + 'px' }"
             class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-2xl shadow-2xl p-2 flex gap-1" @click.stop>
          <button v-for="emoji in quickReactions" :key="emoji" @click="sendReaction(emoji)"
                  class="w-10 h-10 flex items-center justify-center rounded-xl text-xl hover:bg-slate-100 dark:hover:bg-slate-800 active:scale-90 transition-all">
            {{ emoji }}
          </button>
        </div>
      </div>
    </Transition>

    <!-- МОДАЛКА МЕДИА И ФАЙЛОВ -->
    <Transition name="fade">
      <div v-if="attachmentsModal" @click="attachmentsModal = null" class="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
        <div @click.stop class="surface-sheet rounded-2xl w-full max-w-md max-h-[80vh] flex flex-col overflow-hidden">
          <div class="p-4 border-b border-slate-200/80 dark:border-white/[0.08] flex items-center justify-between">
            <div>
              <h3 class="font-semibold text-sm">{{ attachmentsModal.chatName }}</h3>
              <div class="flex gap-2 mt-2">
                <button @click="attachmentsModal.tab = 'media'"
                        :class="['px-3 py-1 rounded-lg text-xs font-medium transition-colors', attachmentsModal.tab === 'media' ? 'bg-[#3D7A5E] text-white' : 'bg-black/[0.04] dark:bg-white/[0.05] text-slate-500']">
                  Медиа
                </button>
                <button @click="attachmentsModal.tab = 'files'"
                        :class="['px-3 py-1 rounded-lg text-xs font-medium transition-colors', attachmentsModal.tab === 'files' ? 'bg-[#3D7A5E] text-white' : 'bg-black/[0.04] dark:bg-white/[0.05] text-slate-500']">
                  Файлы
                </button>
              </div>
            </div>
            <button @click="attachmentsModal = null" class="w-8 h-8 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 flex items-center justify-center text-slate-400">
              <X class="w-4 h-4" />
            </button>
          </div>
          <div class="flex-1 overflow-y-auto thin-scroll p-3">
            <div v-if="modalAttachmentMessages.length === 0" class="py-12 text-center text-xs text-slate-400">
              {{ attachmentsModal.tab === 'media' ? 'В этом чате пока нет фото и видео' : 'В этом чате пока нет файлов' }}
            </div>
            <div v-else-if="attachmentsModal.tab === 'media'" class="grid grid-cols-3 gap-2">
              <div v-for="m in modalAttachmentMessages" :key="m.id" @click="scrollToMessage(m.id); attachmentsModal = null"
                   class="aspect-square rounded-xl overflow-hidden bg-black/10 cursor-pointer hover:opacity-85 transition-opacity">
                <img v-if="m.type === 'Image' && m.attachmentUrl" :src="m.attachmentUrl" class="w-full h-full object-cover">
                <div v-else class="w-full h-full flex items-center justify-center text-slate-400">
                  <Video class="w-6 h-6" />
                </div>
              </div>
            </div>
            <div v-else class="space-y-2">
              <div v-for="m in modalAttachmentMessages" :key="m.id" @click="scrollToMessage(m.id); attachmentsModal = null"
                   class="flex items-center gap-3 p-2.5 rounded-xl bg-black/[0.03] dark:bg-white/[0.04] hover:bg-black/[0.06] cursor-pointer">
                <FileText class="w-5 h-5 text-[#3D7A5E] shrink-0" />
                <div class="min-w-0 flex-1">
                  <p class="text-xs font-medium truncate">{{ m.content || 'Файл' }}</p>
                  <p class="text-[10px] text-slate-400">{{ m.username }} · {{ formatTime(m.sentAt) }}</p>
                </div>
              </div>
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
.highlight-message { animation: highlight 1.5s ease; }
@keyframes highlight {
  0%   { background-color: transparent; }
  20%  { background-color: rgba(61, 122, 94, 0.25); }
  100% { background-color: transparent; }
}
</style>