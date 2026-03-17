<script setup lang="ts">
import { ref, computed } from 'vue';
import { useChatStore } from '../store/chat';

const emit = defineEmits<{
  sendSticker: [stickerId: string];
  close: [];
}>();

const chat = useChatStore();

const activeStickerPackId = ref<string | null>(null);
const isCreatePackOpen = ref(false);
const newPackName = ref('');
const creatingPack = ref(false);
const uploadingSticker = ref(false);

const activePack = computed(() =>
  chat.stickerPacks.find(p => p.id === activeStickerPackId.value) || chat.stickerPacks[0] || null
);

const selectPack = (packId: string) => {
  activeStickerPackId.value = packId;
  isCreatePackOpen.value = false;
};

const sendSticker = async (stickerId: string) => {
  emit('sendSticker', stickerId);
  emit('close');
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
</script>

<template>
  <div class="mb-2 bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden shadow-2xl"
       @click.stop>
    <div class="flex h-64">
      <!-- Список паков -->
      <div class="w-14 bg-slate-50 dark:bg-slate-900 border-r border-slate-200 dark:border-slate-800 flex flex-col items-center py-2 gap-2 overflow-y-auto shrink-0">
        <button v-for="pack in chat.stickerPacks" :key="pack.id" @click="selectPack(pack.id)"
                :class="['w-10 h-10 rounded-xl overflow-hidden border-2 transition-all active:scale-90',
                  activePack?.id === pack.id ? 'border-purple-500' : 'border-transparent']">
          <img v-if="pack.coverUrl" :src="pack.coverUrl" class="w-full h-full object-cover">
          <div v-else class="w-full h-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-xs font-bold">
            {{ pack.name[0] }}
          </div>
        </button>
        <button @click="isCreatePackOpen = !isCreatePackOpen; activeStickerPackId = null"
                :class="['w-10 h-10 rounded-xl border-2 flex items-center justify-center transition-all active:scale-90',
                  isCreatePackOpen
                    ? 'border-purple-500 bg-purple-600/20 text-purple-400'
                    : 'border-slate-200 dark:border-slate-700 text-slate-400 dark:text-slate-500 hover:border-slate-300 dark:hover:border-slate-600']">
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
          </svg>
        </button>
      </div>

      <!-- Содержимое пака -->
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
            <button v-for="sticker in activePack.stickers" :key="sticker.id"
                    @click="sendSticker(sticker.id)"
                    class="w-full aspect-square rounded-xl overflow-hidden hover:bg-slate-100 dark:hover:bg-slate-700 active:scale-90 transition-all p-1">
              <img :src="sticker.url" class="w-full h-full object-contain" draggable="false" oncontextmenu="return false">
            </button>
            <div v-if="activePack.stickers.length === 0"
                 class="col-span-4 flex flex-col items-center justify-center py-8 opacity-30">
              <p class="text-xs">Нет стикеров</p>
              <p v-if="activePack.createdByMe" class="text-xs mt-1">Нажми + чтобы добавить</p>
            </div>
          </div>
        </div>

        <div v-else-if="!isCreatePackOpen"
             class="flex flex-col items-center justify-center h-full opacity-30 p-4 text-center">
          <p class="text-2xl mb-2">🎭</p>
          <p class="text-xs">Нет стикеров. Нажми + чтобы создать пак</p>
        </div>
      </div>
    </div>
  </div>
</template>