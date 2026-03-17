import { ref } from 'vue';

const isDark = ref(true);

export function useTheme() {
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

  return { isDark, initTheme, toggleTheme };
}