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
  <div class="relative w-full">
    <!-- Превью ответа -->
    <div v-if="msg.replyToUsername"
         @click="msg.replyToMessageId && scrollToMessage(msg.replyToMessageId)"
         class="mb-1.5 px-3 py-1.5 rounded-xl border-l-2 text-xs opacity-80 max-w-full cursor-pointer hover:opacity-100 transition-opacity flex items-center gap-2"
         :style="{
           borderLeftColor: 'var(--chat-accent)',
           background: isOwn ? 'rgba(var(--chat-accent-rgb), 0.12)' : 'var(--sheet-subtle)'
         }">
      <span class="w-1 h-3.5 rounded-full shrink-0" style="background: var(--chat-accent)"></span>
      <div class="min-w-0">
        <p class="font-semibold truncate text-[11.5px]" style="color: var(--chat-accent)">{{ msg.replyToUsername }}</p>
        <p class="truncate text-dim text-[11px]">{{ msg.replyToContent }}</p>
      </div>
    </div>

    <!-- Стикер -->
    <div v-if="msg.type === 'Sticker' && msg.attachmentUrl"
         @click="msg.packId && onStickerClick(msg.packId)"
         class="cursor-pointer active:scale-95 transition-transform relative inline-block">
      <img :src="msg.attachmentUrl" class="w-32 h-32 object-contain rounded-2xl" draggable="false" oncontextmenu="return false">
      <span v-if="isOwn" class="absolute bottom-1 right-1 opacity-80" style="color: var(--chat-accent)">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-current stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-current stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Изображение -->
    <div v-else-if="msg.type === 'Image' && msg.attachmentUrl" class="relative inline-block">
      <img v-if="getResolvedUrl(msg.attachmentUrl)"
           :src="getResolvedUrl(msg.attachmentUrl)"
           class="max-w-xs rounded-2xl object-cover cursor-pointer active:scale-95 transition-transform border border-hairline shadow-xs"
           draggable="false" oncontextmenu="return false"
           @click="openMedia(getResolvedUrl(msg.attachmentUrl), 'Image')">
      <div v-else class="max-w-xs h-32 rounded-2xl surface-subtle flex items-center justify-center">
        <div class="w-5 h-5 border-2 border-t-transparent rounded-full animate-spin" style="border-color: var(--chat-accent); border-top-color: transparent"></div>
      </div>
      <span v-if="isOwn" class="absolute bottom-1.5 right-1.5 bg-black/40 backdrop-blur-xs rounded-full px-1 py-0.5" style="color: #10B981">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-emerald-400 stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Видео -->
    <div v-else-if="msg.type === 'Video' && msg.attachmentUrl" class="relative inline-block">
      <div v-if="getResolvedUrl(msg.attachmentUrl)"
           class="relative max-w-xs cursor-pointer group"
           @click="openMedia(getResolvedUrl(msg.attachmentUrl), 'Video')">
        <video :src="getResolvedUrl(msg.attachmentUrl)"
               class="max-w-xs rounded-2xl pointer-events-none border border-hairline"></video>
        <div class="absolute inset-0 flex items-center justify-center rounded-2xl bg-black/30 group-hover:bg-black/40 transition-colors">
          <div class="w-11 h-11 rounded-full bg-white/20 backdrop-blur-sm flex items-center justify-center text-white">
            <svg viewBox="0 0 24 24" class="w-5 h-5 fill-white ml-0.5"><path d="M8 5v14l11-7z"/></svg>
          </div>
        </div>
      </div>
      <div v-else class="max-w-xs h-32 rounded-2xl surface-subtle flex items-center justify-center">
        <div class="w-5 h-5 border-2 border-t-transparent rounded-full animate-spin" style="border-color: var(--chat-accent); border-top-color: transparent"></div>
      </div>
    </div>

    <!-- Голосовое -->
    <AudioPlayer
      v-else-if="msg.type === 'Voice' && msg.attachmentUrl"
      :url="msg.attachmentUrl"
      :is-own="isOwn"
      :is-read="msg.isRead ?? false"
    />

    <!-- Обычное текстовое сообщение (в стиле референса «Слой») -->
    <div v-else
      class="px-3.5 py-2.5 rounded-2xl text-[13.5px] break-words leading-relaxed transition-colors border"
      :style="isOwn ? {
        background: 'var(--own-msg-bg)',
        borderColor: 'rgba(var(--chat-accent-rgb), 0.3)',
        borderLeftWidth: '3px',
        borderLeftColor: 'var(--chat-accent)',
        color: 'var(--text-ink)'
      } : {
        background: 'var(--sheet-subtle)',
        borderColor: 'var(--border-hairline)',
        color: 'var(--text-ink)'
      }">
      <span>{{ msg.content }}</span>
      <!-- Индикатор прочтения для своих сообщений -->
      <span v-if="isOwn" class="inline-flex items-center ml-2.5 translate-y-0.5 opacity-90" style="color: var(--chat-accent)">
        <svg v-if="!msg.isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-current stroke-2"><path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-current stroke-2"><path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/><path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </span>
    </div>

    <!-- Реакции -->
    <div v-if="msg.reactions && msg.reactions.length > 0"
         :class="['flex flex-wrap gap-1 mt-1.5', isOwn ? 'justify-end' : 'justify-start']">
      <button v-for="r in msg.reactions" :key="r.emoji"
              @click="addReaction(msg.id, r.emoji)"
              :class="['flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs transition-all active:scale-90 border']"
              :style="r.userIds.some(id => id.toLowerCase() === currentUserId.toLowerCase()) ? {
                background: 'rgba(var(--chat-accent-rgb), 0.16)',
                borderColor: 'var(--chat-accent)',
                color: 'var(--text-ink)'
              } : {
                background: 'var(--sheet-subtle)',
                borderColor: 'var(--border-hairline)',
                color: 'var(--text-secondary)'
              }">
        <span class="text-[12px]">{{ r.emoji }}</span>
        <span class="font-bold text-[11px]">{{ r.count }}</span>
      </button>
    </div>

    <!-- Лайтбокс -->
    <MediaViewer v-if="viewer"
                 :url="viewer.url"
                 :type="viewer.type"
                 @close="viewer = null" />
  </div>
</template>
