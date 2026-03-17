<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import api from '../api';

const props = defineProps<{
  url: string;
  isOwn: boolean;
  isRead: boolean;
}>();

const BARS = 40;

const isPlaying = ref(false);
const currentTime = ref(0);
const duration = ref(0);
const isLoaded = ref(false);
const isLoading = ref(false);
const waveHeights = ref<number[]>(Array(BARS).fill(0.15));

let audio: HTMLAudioElement | null = null;
let blobUrl: string | null = null;
let animFrame: number | null = null;

const formatTime = (s: number) => {
  if (!s || isNaN(s) || !isFinite(s)) return '0:00';
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, '0')}`;
};

const displayTime = computed(() => {
  if (!isLoaded.value) return '0:00';
  const remaining = duration.value - currentTime.value;
  return formatTime(remaining > 0 ? remaining : 0);
});

const progress = computed(() => {
  if (!duration.value) return 0;
  return currentTime.value / duration.value;
});

const tick = () => {
  if (audio) currentTime.value = audio.currentTime;
  if (isPlaying.value) animFrame = requestAnimationFrame(tick);
};

// Генерируем waveform из реальных данных через Web Audio API
const generateWaveform = async (arrayBuffer: ArrayBuffer) => {
  try {
    const audioCtx = new AudioContext();
    const decoded = await audioCtx.decodeAudioData(arrayBuffer.slice(0));
    await audioCtx.close();

    const channelData = decoded.getChannelData(0);
    const blockSize = Math.floor(channelData.length / BARS);
    const heights: number[] = [];

    for (let i = 0; i < BARS; i++) {
      let sum = 0;
      const start = i * blockSize;
      for (let j = start; j < start + blockSize; j++) {
        sum += Math.abs(channelData[j] ?? 0);
      }
      heights.push(sum / blockSize);
    }

    // Нормализуем от 0.1 до 1
    const max = Math.max(...heights, 0.001);
    waveHeights.value = heights.map(h => Math.max(0.1, h / max));
  } catch {
    // Fallback — синусоида если Web Audio не доступен
    waveHeights.value = Array.from({ length: BARS }, (_, i) => {
      const v = Math.abs(Math.sin(i * 0.7) * 0.4 + Math.sin(i * 0.3) * 0.35 + Math.sin(i * 1.2) * 0.25);
      return Math.max(0.1, Math.min(1, v + 0.15));
    });
  }
};

const loadAudio = async () => {
  if (audio || isLoading.value) return;
  isLoading.value = true;

  try {
    // Загружаем через axios с токеном
    const path = new URL(props.url).pathname.replace('/api', '');
    const response = await api.get(path, { responseType: 'arraybuffer' });
    const arrayBuffer: ArrayBuffer = response.data;

    // Создаём blob URL для HTMLAudioElement
    const blob = new Blob([arrayBuffer], { type: 'audio/webm' });
    blobUrl = URL.createObjectURL(blob);

    // Генерируем waveform из тех же данных
    await generateWaveform(arrayBuffer);

    // Инициализируем аудио
    audio = new Audio(blobUrl);
    await new Promise<void>((resolve) => {
      audio!.addEventListener('loadedmetadata', () => {
        duration.value = audio!.duration;
        isLoaded.value = true;
        resolve();
      });
      audio!.addEventListener('error', () => resolve());
      audio!.load();
    });

    audio.addEventListener('ended', () => {
      isPlaying.value = false;
      currentTime.value = 0;
      if (animFrame) { cancelAnimationFrame(animFrame); animFrame = null; }
    });

    audio.addEventListener('timeupdate', () => {
      currentTime.value = audio!.currentTime;
    });
  } catch (e) {
    console.error('Ошибка загрузки аудио:', e);
  } finally {
    isLoading.value = false;
  }
};

const togglePlay = async () => {
  if (!audio) await loadAudio();
  if (!audio) return;

  if (isPlaying.value) {
    audio.pause();
    isPlaying.value = false;
  } else {
    await audio.play();
    isPlaying.value = true;
  }
};

const seek = async (e: MouseEvent) => {
  if (!audio) await loadAudio();
  if (!audio || !duration.value) return;
  const bar = e.currentTarget as HTMLElement;
  const ratio = Math.max(0, Math.min(1, e.offsetX / bar.offsetWidth));
  audio.currentTime = ratio * duration.value;
  currentTime.value = audio.currentTime;
};

onMounted(() => {
  // Загружаем сразу чтобы показать duration и waveform
  loadAudio();
});

onUnmounted(() => {
  audio?.pause();
  if (animFrame) cancelAnimationFrame(animFrame);
  if (blobUrl) URL.revokeObjectURL(blobUrl);
});
</script>

<template>
  <div :class="['flex items-center gap-3 px-3 py-2.5 rounded-2xl select-none w-64',
    isOwn
      ? 'bg-gradient-to-r from-purple-600 to-indigo-600'
      : 'bg-slate-200 dark:bg-slate-800']">

    <!-- Кнопка Play/Pause -->
    <button @click="togglePlay"
            :class="['w-9 h-9 rounded-full flex items-center justify-center shrink-0 transition-all active:scale-90',
              isOwn
                ? 'bg-white/20 hover:bg-white/30'
                : 'bg-purple-500 hover:bg-purple-600']">
      <!-- Загрузка -->
      <div v-if="isLoading" class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
      <!-- Play -->
      <svg v-else-if="!isPlaying" viewBox="0 0 24 24" class="w-4 h-4 fill-white ml-0.5">
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
      <div class="relative h-8 cursor-pointer flex items-end gap-px mb-1"
           @click="seek">
        <div v-for="(h, i) in waveHeights" :key="i"
             :class="['rounded-sm transition-colors duration-75 shrink-0',
               i / BARS <= progress
                 ? (isOwn ? 'bg-white' : 'bg-purple-500')
                 : (isOwn ? 'bg-white/30' : 'bg-slate-400 dark:bg-slate-500')]"
             :style="{ width: '3px', height: `${Math.round(h * 28) + 2}px` }">
        </div>
      </div>

      <!-- Время -->
      <span :class="['text-[11px] font-medium tabular-nums',
        isOwn ? 'text-white/90' : 'text-slate-600 dark:text-slate-400']">
        {{ displayTime }}
      </span>
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