<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import api from '../api';

const props = defineProps<{
  url: string;
  isOwn: boolean;
  isRead: boolean;
}>();

const BARS = 28;

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

    const max = Math.max(...heights, 0.001);
    waveHeights.value = heights.map(h => Math.max(0.12, h / max));
  } catch {
    waveHeights.value = Array.from({ length: BARS }, (_, i) => {
      const v = Math.abs(Math.sin(i * 0.7) * 0.4 + Math.sin(i * 0.3) * 0.35 + Math.sin(i * 1.2) * 0.25);
      return Math.max(0.12, Math.min(1, v + 0.15));
    });
  }
};

const loadAudio = async () => {
  if (audio || isLoading.value) return;
  isLoading.value = true;

  try {
    const path = new URL(props.url).pathname.replace('/api', '');
    const response = await api.get(path, { responseType: 'arraybuffer' });
    const arrayBuffer: ArrayBuffer = response.data;

    const blob = new Blob([arrayBuffer], { type: 'audio/webm' });
    blobUrl = URL.createObjectURL(blob);

    await generateWaveform(arrayBuffer);

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
    animFrame = requestAnimationFrame(tick);
  }
};

const seek = async (e: MouseEvent) => {
  if (!audio) await loadAudio();
  if (!audio || !duration.value) return;
  const bar = e.currentTarget as HTMLElement;
  const rect = bar.getBoundingClientRect();
  const ratio = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
  audio.currentTime = ratio * duration.value;
  currentTime.value = audio.currentTime;
};

onMounted(() => {
  loadAudio();
});

onUnmounted(() => {
  audio?.pause();
  if (animFrame) cancelAnimationFrame(animFrame);
  if (blobUrl) URL.revokeObjectURL(blobUrl);
});
</script>

<template>
  <div class="flex items-center gap-3 px-3 py-2.5 rounded-2xl select-none w-64 border transition-all"
       :style="isOwn ? {
         background: 'var(--own-msg-bg)',
         borderColor: 'rgba(var(--chat-accent-rgb), 0.35)',
         borderLeftWidth: '3px',
         borderLeftColor: 'var(--chat-accent)'
       } : {
         background: 'var(--sheet-subtle)',
         borderColor: 'var(--border-hairline)'
       }">

    <!-- Кнопка Play/Pause -->
    <button @click="togglePlay"
            class="w-9 h-9 rounded-full flex items-center justify-center shrink-0 transition-all active:scale-95 text-white"
            style="background: var(--chat-accent);">
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
      <div class="relative h-7 cursor-pointer flex items-end justify-between gap-[2px] mb-1 overflow-hidden"
           @click="seek">
        <div v-for="(h, i) in waveHeights" :key="i"
             class="rounded-full transition-colors duration-75 flex-1 min-w-[2px] max-w-[3px]"
             :style="{
               height: `${Math.round(h * 24) + 3}px`,
               background: i / BARS <= progress
                 ? 'var(--chat-accent)'
                 : 'var(--border-strong)'
             }">
        </div>
      </div>

      <!-- Время -->
      <span class="text-[11px] font-mono-meta text-dim tabular-nums">
        {{ displayTime }}
      </span>
    </div>

    <!-- Галочки прочтения для своих -->
    <span v-if="isOwn" class="opacity-80 shrink-0 self-end mb-0.5" style="color: var(--chat-accent);">
      <svg v-if="!isRead" viewBox="0 0 24 24" class="w-3.5 h-3.5 fill-none stroke-current stroke-2">
        <path d="M4 12L9 17L20 6" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
      <svg v-else viewBox="0 0 24 24" class="w-4 h-3.5 fill-none stroke-current stroke-2">
        <path d="M2 12L7 17L18 6" stroke-linecap="round" stroke-linejoin="round"/>
        <path d="M8 12L13 17L24 6" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </span>
  </div>
</template>
