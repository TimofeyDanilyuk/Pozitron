<script setup lang="ts">
import { ref, computed } from 'vue';

const emit = defineEmits<{ select: [emoji: string] }>();

const search = ref('');

const emojiCategories = [
  { label: '😀 Смайлы', emojis: ['😀','😁','😂','🤣','😃','😄','😅','😆','😉','😊','😋','😎','😍','🥰','😘','😗','😙','😚','🙂','🤗','🤩','🤔','🤨','😐','😑','😶','🙄','😏','😣','😥','😮','🤐','😯','😪','😫','😴','😌','😛','😜','😝','🤤','😒','😓','😔','😕','🙃','🤑','😲','☹️','🙁','😖','😞','😟','😤','😢','😭','😦','😧','😨','😩','🤯','😬','😰','😱','🥵','🥶','😳','🤪','😵','🥴','😠','😡','🤬','😷','🤒','🤕','🤧','🥱'] },
  { label: '👋 Жесты', emojis: ['👋','🤚','🖐️','✋','🖖','👌','🤌','🤏','✌️','🤞','🤟','🤘','🤙','👈','👉','👆','🖕','👇','☝️','👍','👎','✊','👊','🤛','🤜','👏','🙌','👐','🤲','🤝','🙏','✍️','💅','🤳','💪','🦾'] },
  { label: '❤️ Сердца', emojis: ['❤️','🧡','💛','💚','💙','💜','🖤','🤍','🤎','💔','❣️','💕','💞','💓','💗','💖','💘','💝','💟'] },
  { label: '🐶 Животные', emojis: ['🐶','🐱','🐭','🐹','🐰','🦊','🐻','🐼','🐨','🐯','🦁','🐮','🐷','🐸','🐵','🙈','🙉','🙊','🐒','🐔','🐧','🐦','🦆','🦅','🦉','🦇','🐺','🐗','🐴','🦄','🐝','🦋','🐌','🐞','🐜','🐢','🐍','🦎','🐙','🦑','🐡','🐠','🐟','🐬','🐳','🦈','🐊','🐅','🐆','🦓','🦍','🐘','🦒','🦘','🐕','🐈','🐇','🦝','🦦','🦥','🐁','🐀','🐿️','🦔'] },
  { label: '🍕 Еда', emojis: ['🍏','🍎','🍊','🍋','🍌','🍉','🍇','🍓','🫐','🍒','🍑','🥭','🍍','🥥','🥝','🍅','🥑','🥦','🌶️','🧄','🧅','🥔','🍔','🍟','🍕','🌭','🥪','🌮','🌯','🍝','🍜','🍲','🍣','🍱','🍤','🍙','🍚','🎂','🍰','🧁','🍩','🍪','🍫','🍬','🍭','🍿','☕','🍵','🧋','🍺','🍻','🥂','🍷','🥃','🍸','🍹'] },
  { label: '⚽ Спорт', emojis: ['⚽','🏀','🏈','⚾','🎾','🏐','🏉','🎱','🏓','🏸','🥊','🥋','🎯','🏆','🥇','🥈','🥉','🏅','🎖️','🎮','🕹️','♟️','🧩'] },
  { label: '🚀 Разное', emojis: ['🚗','🚕','✈️','🚀','🌍','🏠','💡','🔥','⭐','🌟','💫','❤️‍🔥','🎉','🎊','🎁','🏆','🎯','🎲','🎨','🎬','🎤','🎧','🎵','🎶'] },
];

const filtered = computed(() => {
  if (!search.value.trim()) return emojiCategories;
  return [{ label: '🔍 Результаты', emojis: emojiCategories.flatMap(c => c.emojis) }];
});

const onSelect = (emoji: string) => {
  emit('select', emoji);
  search.value = '';
};
</script>

<template>
  <div class="bg-white dark:bg-slate-950 border border-slate-200 dark:border-slate-700 rounded-2xl overflow-hidden shadow-2xl"
       @click.stop>
    <div class="p-2 border-b border-slate-200 dark:border-slate-800">
      <input v-model="search" type="text" placeholder="Поиск эмодзи..."
             class="w-full bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-3 py-2 text-xs outline-none select-text text-slate-900 dark:text-slate-100">
    </div>
    <div class="max-h-48 overflow-y-auto p-2 space-y-3">
      <div v-for="cat in filtered" :key="cat.label">
        <p class="text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase px-1 mb-1">{{ cat.label }}</p>
        <div class="flex flex-wrap gap-0.5">
          <button v-for="emoji in cat.emojis" :key="emoji" @click="onSelect(emoji)"
                  class="w-9 h-9 flex items-center justify-center rounded-lg text-xl hover:bg-slate-100 dark:hover:bg-slate-700 active:scale-90 transition-all">
            {{ emoji }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>