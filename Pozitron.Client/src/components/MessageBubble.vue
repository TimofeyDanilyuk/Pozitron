<script setup lang="ts">
import { ref } from 'vue';
import AudioPlayer from './AudioPlayer.vue';
import MediaViewer from './MediaViewer.vue';
import { useBlobCache } from '../composables/useBlobCache';
import type { Message } from '../store/chat';

const props = defineProps<{
  msg: Message;
  isOwn: boolean;
  onStickerClick: (packId: string) => void;
  onReply: (msg: Message) => void;
  onReact: (msgId: string) => void;
  scrollToMessage: (msgId: string) => void;
  addReaction: (msgId: string, emoji: string) => void;
  currentUserId: string;
}>();

const { getResolvedUrl } = useBlobCache();

const viewer = ref<{ url: string; type: 'Image' | 'Video' } | null>(null);
const openMedia = (url: string, type: 'Image' | 'Video') => {
  viewer.value = { url, type };
};
</script>

<template>
  <div class="relative">
    <!-- Превью ответа -->
    <div v-if="msg.replyToUsername"
         @click="msg.replyToMessageId && scrollToMessage(msg.replyToMessageId)"
         :class="['mb-1 px-2 py-1 rounded-xl border-l-2 border-purple-400 text-xs opacity-70 max-w-full cursor-pointer hover:opacity-100 transition-opacity',
           isOwn ? 'bg-white/10' : 'bg-slate-300/50 dark:bg-slate-700/50']">
      <p class="font-bold text-purple-400 truncate">{{ msg.replyToUsername }}</p>
      <p class="truncate text-slate-600 dark:text-slate-300">{{ msg.replyToContent }}</p>
    </div>

    <!-- Стикер -->
    <div v-if="msg.type === 'Sticker' && msg.attachmentUrl"
         @click="msg.packId && onStickerClick(msg.packId)"
         class="cursor-pointer active:scale-95 transition-transform relative">
      <img :src="msg.attachmentUrl" class="w-32 h-32 object-contain rounded-2xl" draggable="false" oncontextmenu="return false">
      <span v-if="isOwn" class="absolute bottom-1 right-1 opacity-70">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Изображение -->
    <div v-else-if="msg.type === 'Image' && msg.attachmentUrl" class="relative">
      <img v-if="getResolvedUrl(msg.attachmentUrl)"
           :src="getResolvedUrl(msg.attachmentUrl)"
           class="max-w-xs rounded-2xl object-cover cursor-pointer active:scale-95 transition-transform"
           draggable="false" oncontextmenu="return false"
           @click="openMedia(getResolvedUrl(msg.attachmentUrl), 'Image')">
      <div v-else class="max-w-xs h-32 rounded-2xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center">
        <div class="w-5 h-5 border-2 border-purple-400 border-t-transparent rounded-full animate-spin"></div>
      </div>
      <span v-if="isOwn" class="absolute bottom-1 right-1 opacity-70">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Видео -->
    <div v-else-if="msg.type === 'Video' && msg.attachmentUrl" class="relative">
      <div v-if="getResolvedUrl(msg.attachmentUrl)"
           class="relative max-w-xs cursor-pointer group"
           @click="openMedia(getResolvedUrl(msg.attachmentUrl), 'Video')">
        <video :src="getResolvedUrl(msg.attachmentUrl)"
               class="max-w-xs rounded-2xl pointer-events-none"></video>
        <!-- Кнопка play поверх видео -->
        <div class="absolute inset-0 flex items-center justify-center rounded-2xl bg-black/30 group-hover:bg-black/40 transition-colors">
          <div class="w-12 h-12 rounded-full bg-white/20 backdrop-blur-sm flex items-center justify-center">
            <svg viewBox="0 0 24 24" class="w-6 h-6 fill-white ml-0.5"><path d="M8 5v14l11-7z"/></svg>
          </div>
        </div>
      </div>
      <div v-else class="max-w-xs h-32 rounded-2xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center">
        <div class="w-5 h-5 border-2 border-purple-400 border-t-transparent rounded-full animate-spin"></div>
      </div>
      <span v-if="isOwn" class="absolute bottom-1 right-1 opacity-70">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Голосовое -->
    <AudioPlayer
      v-else-if="msg.type === 'Voice' && msg.attachmentUrl"
      :url="msg.attachmentUrl"
      :is-own="isOwn"
      :is-read="msg.isRead ?? false"
    />

    <!-- Обычное сообщение -->
    <div v-else :class="[
      'px-3 py-2 rounded-2xl text-sm break-words leading-relaxed',
      isOwn
        ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white rounded-br-sm'
        : 'bg-slate-200 dark:bg-slate-800 text-slate-900 dark:text-slate-100 rounded-bl-sm'
    ]">
      {{ msg.content }}
      <span v-if="isOwn" class="inline-flex items-center ml-2 opacity-70 translate-y-0.5">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Реакции -->
    <div v-if="msg.reactions && msg.reactions.length > 0"
         :class="['flex flex-wrap gap-1 mt-1', isOwn ? 'justify-end' : 'justify-start']">
      <button v-for="r in msg.reactions" :key="r.emoji"
              @click="addReaction(msg.id, r.emoji)"
              :class="['flex items-center gap-1 px-2 py-0.5 rounded-full text-xs transition-all active:scale-90 border',
                r.userIds.some(id => id.toLowerCase() === currentUserId.toLowerCase())
                  ? 'bg-purple-100 dark:bg-purple-600/30 border-purple-400 text-purple-700 dark:text-purple-300'
                  : 'bg-slate-100 dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300']">
        <span>{{ r.emoji }}</span>
        <span class="font-bold">{{ r.count }}</span>
      </button>
    </div>

    <!-- Лайтбокс -->
    <MediaViewer v-if="viewer"
                 :url="viewer.url"
                 :type="viewer.type"
                 @close="viewer = null" />
  </div>
</template>