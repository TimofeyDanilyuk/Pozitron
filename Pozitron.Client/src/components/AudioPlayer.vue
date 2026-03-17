<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';

const props = defineProps<{
  url: string;
  isOwn: boolean;
  isRead: boolean;
}>();

// Статичный waveform — 30 баров с псевдослучайными высотами
const BARS = 30;
const waveHeights = Array.from({ length: BARS }, (_, i) => {
  // Генерируем высоты на основе индекса — выглядит как реальная волна
  const base = Math.sin(i * 0.8) * 0.3 + Math.sin(i * 0.3) * 0.4 + Math.sin(i * 1.5) * 0.2;
  return Math.max(0.15, Math.min(1, Math.abs(base) + 0.2));
});

const isPlaying = ref(false);
const currentTime = ref(0);
const duration = ref(0);
const isLoaded = ref(false);

let audio: HTMLAudioElement | null = null;
let animFrame: number | null = null;

const formatTime = (s: number) => {
  if (!s || isNaN(s)) return '0:00';
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, '0')}`;
};

const displayTime = () => {
  if (!isLoaded.value) return '0:00';
  if (isPlaying.value || currentTime.value > 0) {
    // Показываем оставшееся время как в Telegram
    return formatTime(duration.value - currentTime.value);
  }
  return formatTime(duration.value);
};

const progress = () => {
  if (!duration.value) return 0;
  return currentTime.value / duration.value;
};

const tick = () => {
  if (audio) currentTime.value = audio.currentTime;
  if (isPlaying.value) animFrame = requestAnimationFrame(tick);
};

const initAudio = () => {
  if (audio) return;
  audio = new Audio(props.url);
  audio.addEventListener('loadedmetadata', () => {
    duration.value = audio!.duration;
    isLoaded.value = true;
  });
  audio.addEventListener('ended', () => {
    isPlaying.value = false;
    currentTime.value = 0;
    if (animFrame) cancelAnimationFrame(animFrame);
  });
  audio.load();
};

const togglePlay = () => {
  initAudio();
  if (!audio) return;
  if (isPlaying.value) {
    audio.pause();
    isPlaying.value = false;
    if (animFrame) cancelAnimationFrame(animFrame);
  } else {
    audio.play();
    isPlaying.value = true;
    animFrame = requestAnimationFrame(tick);
  }
};

const seek = (e: MouseEvent) => {
  initAudio();
  if (!audio || !duration.value) return;
  const bar = e.currentTarget as HTMLElement;
  const ratio = Math.max(0, Math.min(1, e.offsetX / bar.offsetWidth));
  audio.currentTime = ratio * duration.value;
  currentTime.value = audio.currentTime;
};

onMounted(() => {
  // Инициализируем аудио сразу чтобы получить duration
  initAudio();
});

onUnmounted(() => {
  audio?.pause();
  if (animFrame) cancelAnimationFrame(animFrame);
});
</script>

<template>
  <div :class="['flex items-center gap-3 px-3 py-2.5 rounded-2xl select-none',
    isOwn
      ? 'bg-gradient-to-r from-purple-600 to-indigo-600 w-64'
      : 'bg-slate-200 dark:bg-slate-800 w-64']">

    <!-- Кнопка Play/Pause -->
    <button @click="togglePlay"
            :class="['w-9 h-9 rounded-full flex items-center justify-center shrink-0 transition-all active:scale-90',
              isOwn
                ? 'bg-white/20 hover:bg-white/30'
                : 'bg-purple-500 hover:bg-purple-600']">
      <!-- Play -->
      <svg v-if="!isPlaying" viewBox="0 0 24 24" class="w-4 h-4 fill-white ml-0.5">
        <path d="M8 5v14l11-7z"/>
      </svg>
      <!-- Pause -->
      <svg v-else viewBox="0 0 24 24" class="w-4 h-4 fill-white">
        <path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/>
      </svg>
    </button>

    <!-- Waveform + время -->
    <div class="flex-1 min-w-0">
      <!-- Waveform bars -->
      <div class="relative h-8 cursor-pointer flex items-center gap-px mb-0.5"
           @click="seek">
        <div v-for="(h, i) in waveHeights" :key="i"
             :class="['rounded-full transition-colors duration-100 shrink-0',
               i / BARS <= progress()
                 ? (isOwn ? 'bg-white' : 'bg-purple-500')
                 : (isOwn ? 'bg-white/30' : 'bg-slate-400 dark:bg-slate-600')]"
             :style="{ width: '3px', height: `${h * 100}%`, minHeight: '3px' }">
        </div>
      </div>

      <!-- Время -->
      <div class="flex items-center justify-between">
        <span :class="['text-[11px] font-medium tabular-nums',
          isOwn ? 'text-white/90' : 'text-slate-600 dark:text-slate-400']">
          {{ displayTime() }}
        </span>
      </div>
    </div>

    <!-- Галочки прочтения -->
    <span v-if="isOwn" class="opacity-80 shrink-0 self-end mb-0.5">
      <svg v-if="!isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-white/80 stroke-2">
        <path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
      <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-white stroke-2">
        <path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/>
        <path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </span>
  </div>
</template>