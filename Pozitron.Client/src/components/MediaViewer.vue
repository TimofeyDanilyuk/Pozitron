<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue';

const props = defineProps<{
  url: string;
  type: 'Image' | 'Video';
}>();

const emit = defineEmits<{ close: [] }>();

const onKeydown = (e: KeyboardEvent) => {
  if (e.key === 'Escape') emit('close');
};

onMounted(() => window.addEventListener('keydown', onKeydown));
onUnmounted(() => window.removeEventListener('keydown', onKeydown));
</script>

<template>
  <Teleport to="body">
    <div class="fixed inset-0 z-[100] flex items-center justify-center"
         @click.self="emit('close')">

      <!-- Фон -->
      <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="emit('close')"></div>

      <!-- Контент -->
      <div class="relative z-10 max-w-[90vw] max-h-[90vh] flex items-center justify-center">

        <!-- Изображение -->
        <img v-if="type === 'Image'"
             :src="url"
             class="max-w-[90vw] max-h-[90vh] rounded-2xl object-contain shadow-2xl select-none"
             draggable="false"
             @click.stop>

        <!-- Видео -->
        <video v-else-if="type === 'Video'"
               :src="url"
               controls autoplay
               class="max-w-[90vw] max-h-[90vh] rounded-2xl shadow-2xl"
               @click.stop>
        </video>

        <!-- Кнопка закрыть -->
        <button @click="emit('close')"
                class="absolute -top-4 -right-4 w-9 h-9 rounded-full bg-white/10 hover:bg-white/20 border border-white/20 flex items-center justify-center transition-all active:scale-90 backdrop-blur-sm">
          <svg viewBox="0 0 24 24" class="w-4 h-4 fill-none stroke-white stroke-2">
            <path d="M18 6L6 18M6 6l12 12" stroke-linecap="round"/>
          </svg>
        </button>

      </div>
    </div>
  </Teleport>
</template>