<script setup lang="ts">
import { ref, computed } from 'vue';
import { useAuthStore } from '../store/auth.ts'; 
import { useRouter } from 'vue-router';
import axios from 'axios';

const auth = useAuthStore();
const router = useRouter();

const mode = ref<'login' | 'register' | 'recover'>('login');

const username = ref('');
const password = ref('');
const confirmPassword = ref('');
const securityQuestion = ref('');
const securityAnswer = ref('');
const error = ref('');
const loading = ref(false);

const showPassword = ref(false);
const showConfirmPassword = ref(false);

const recoverStep = ref<1 | 2>(1);
const recoverUsername = ref('');
const recoverQuestion = ref('');
const recoverAnswer = ref('');
const recoverNewPassword = ref('');
const recoverConfirmPassword = ref('');
const showRecoverPassword = ref(false);
const successMessage = ref('');

// ===== СЛОЖНОСТЬ ПАРОЛЯ =====
const calcPasswordScore = (pwd: string): number => {
  let score = 0;
  for (const c of pwd) {
    if (/[a-z]/.test(c)) score += 1;
    else if (/[A-Z]/.test(c)) score += 2;
    else if (/[0-9]/.test(c)) score += 2;
    else score += 3; // спецсимвол
  }
  return score;
};

const passwordScore = computed(() => calcPasswordScore(password.value));

const passwordStrength = computed(() => {
  const s = passwordScore.value;
  const len = password.value.length;
  if (len === 0) return null;
  if (len < 8 || s < 8)  return { label: 'Очень слабый', color: 'bg-red-500',    width: 'w-1/4',  textColor: 'text-red-500' };
  if (s < 12)             return { label: 'Слабый',       color: 'bg-orange-500', width: 'w-2/4',  textColor: 'text-orange-500' };
  if (s < 15)             return { label: 'Средний',      color: 'bg-yellow-500', width: 'w-3/4',  textColor: 'text-yellow-500' };
  if (s < 22)             return { label: 'Надёжный',     color: 'bg-green-500',  width: 'w-full', textColor: 'text-green-500' };
  return                         { label: 'Отличный',     color: 'bg-emerald-500',width: 'w-full', textColor: 'text-emerald-500' };
});

const isPasswordValid = computed(() => password.value.length >= 8 && passwordScore.value >= 15);

const recoverPasswordScore = computed(() => calcPasswordScore(recoverNewPassword.value));
const recoverPasswordStrength = computed(() => {
  const s = recoverPasswordScore.value;
  const len = recoverNewPassword.value.length;
  if (len === 0) return null;
  if (len < 8 || s < 8)  return { label: 'Очень слабый', color: 'bg-red-500',    width: 'w-1/4',  textColor: 'text-red-500' };
  if (s < 12)             return { label: 'Слабый',       color: 'bg-orange-500', width: 'w-2/4',  textColor: 'text-orange-500' };
  if (s < 15)             return { label: 'Средний',      color: 'bg-yellow-500', width: 'w-3/4',  textColor: 'text-yellow-500' };
  if (s < 22)             return { label: 'Надёжный',     color: 'bg-green-500',  width: 'w-full', textColor: 'text-green-500' };
  return                         { label: 'Отличный',     color: 'bg-emerald-500',width: 'w-full', textColor: 'text-emerald-500' };
});

const handleSubmit = async () => {
  error.value = '';
  successMessage.value = '';

  if (mode.value === 'register') {
    const trimmedUsername = username.value.trim();
    if (trimmedUsername.length < 4 || trimmedUsername.length > 32) {
      error.value = 'Ник должен быть от 4 до 32 символов.';
      return;
    }
    if (password.value !== confirmPassword.value) {
      error.value = 'Пароли не совпадают';
      return;
    }
    if (!isPasswordValid.value) {
      error.value = 'Пароль слишком простой. Добавь заглавные буквы, цифры или спецсимволы.';
      return;
    }
    if (!securityQuestion.value.trim() || !securityAnswer.value.trim()) {
      error.value = 'Укажи секретный вопрос и ответ';
      return;
    }
  }

  loading.value = true;
  try {
    if (mode.value === 'login') {
      await auth.login({ username: username.value, password: password.value });
      router.push('/chat');
    } else {
      await auth.register({
        username: username.value,
        password: password.value,
        securityQuestion: securityQuestion.value,
        securityAnswer: securityAnswer.value
      });
      successMessage.value = 'Регистрация прошла успешно! Войдите в аккаунт.';
      setMode('login');
    }
  } catch (e: any) {
    error.value = e.response?.data || 'Ошибка сервера. Попробуйте позже.';
  } finally {
    loading.value = false;
  }
};

const handleRecoverStep1 = async () => {
  error.value = '';
  if (!recoverUsername.value.trim()) {
    error.value = 'Введи ник';
    return;
  }
  loading.value = true;
  try {
    const res = await axios.get(`/api/auth/question/${recoverUsername.value.trim()}`);
    recoverQuestion.value = res.data.question;
    recoverStep.value = 2;
  } catch (e: any) {
    error.value = e.response?.data || 'Пользователь не найден';
  } finally {
    loading.value = false;
  }
};

const handleRecoverStep2 = async () => {
  error.value = '';
  if (recoverNewPassword.value !== recoverConfirmPassword.value) {
    error.value = 'Пароли не совпадают';
    return;
  }
  if (recoverPasswordScore.value < 15 || recoverNewPassword.value.length < 8) {
    error.value = 'Новый пароль слишком простой.';
    return;
  }
  loading.value = true;
  try {
    await axios.post('/api/auth/recover', {
      username: recoverUsername.value.trim(),
      securityAnswer: recoverAnswer.value.trim(),
      newPassword: recoverNewPassword.value
    });
    successMessage.value = 'Пароль успешно изменён! Войдите с новым паролем.';
    setMode('login');
  } catch (e: any) {
    error.value = e.response?.data || 'Неверный ответ';
  } finally {
    loading.value = false;
  }
};

const setMode = (m: 'login' | 'register' | 'recover') => {
  mode.value = m;
  error.value = '';
  password.value = '';
  confirmPassword.value = '';
  securityQuestion.value = '';
  securityAnswer.value = '';
  showPassword.value = false;
  showConfirmPassword.value = false;
  recoverStep.value = 1;
  recoverUsername.value = '';
  recoverQuestion.value = '';
  recoverAnswer.value = '';
  recoverNewPassword.value = '';
  recoverConfirmPassword.value = '';
  showRecoverPassword.value = false;
};
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-linear-to-br from-indigo-500 via-purple-500 to-pink-500 p-4">
    <div class="bg-white/90 dark:bg-slate-800/90 backdrop-blur-md p-8 rounded-3xl shadow-2xl w-full max-w-md border border-white/20">

      <!-- Логотип -->
      <div class="flex justify-center mb-8 pointer-events-none select-none">
        <img src="../assets/login_logo.png" alt="Pozitron Logo" draggable="false"
          class="w-95 h-auto object-contain outline-hidden active:outline-hidden focus:outline-hidden">
      </div>

      <!-- Успех -->
      <div v-if="successMessage" class="mb-4 p-3 bg-green-100 border-l-4 border-green-500 text-green-700 text-sm rounded">
        {{ successMessage }}
      </div>

      <!-- Ошибка -->
      <div v-if="error" class="mb-4 p-3 bg-red-100 border-l-4 border-red-500 text-red-700 text-sm rounded">
        {{ error }}
      </div>

      <!-- ===== ВХОД / РЕГИСТРАЦИЯ ===== -->
      <form v-if="mode !== 'recover'" @submit.prevent="handleSubmit" class="space-y-4">

        <!-- Ник -->
        <div>
          <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Username</label>
          <input v-model="username" type="text" required
            class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
            placeholder="Введите никнейм...">
          <p v-if="mode === 'register' && username.trim().length > 0 && username.trim().length < 4"
             class="text-xs text-red-500 mt-1 ml-1">Минимум 4 символа</p>
        </div>

        <!-- Пароль -->
        <div>
          <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Password</label>
          <div class="relative">
            <input v-model="password" :type="showPassword ? 'text' : 'password'" required
              class="w-full px-4 py-3 pr-12 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
              placeholder="••••••••">
            <button type="button" @click="showPassword = !showPassword"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors p-1">
              <svg v-if="!showPassword" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
              </svg>
              <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
              </svg>
            </button>
          </div>

          <!-- Полоска сложности — только при регистрации -->
          <Transition name="slide">
            <div v-if="mode === 'register' && password.length > 0" class="mt-2 space-y-1">
              <div class="w-full bg-slate-200 dark:bg-slate-700 rounded-full h-1.5 overflow-hidden">
                <div :class="['h-full rounded-full transition-all duration-300', passwordStrength?.color, passwordStrength?.width]"></div>
              </div>
              <div class="flex items-center justify-between">
                <p :class="['text-xs font-medium transition-colors', passwordStrength?.textColor]">
                  {{ passwordStrength?.label }}
                </p>
                <p class="text-xs text-slate-400">
                  {{ passwordScore }} / 15+ очков
                </p>
              </div>
              <div class="text-xs text-slate-400 space-y-0.5">
                <p :class="password.length >= 8 ? 'text-green-500' : ''">
                  {{ password.length >= 8 ? '✓' : '·' }} Минимум 8 символов
                </p>
                <p :class="passwordScore >= 15 ? 'text-green-500' : ''">
                  {{ passwordScore >= 15 ? '✓' : '·' }} Достаточная сложность (нужно 15 очков)
                </p>
              </div>
            </div>
          </Transition>
        </div>

        <!-- Поля только для регистрации -->
        <Transition name="slide">
          <div v-if="mode === 'register'" class="space-y-4">

            <!-- Подтверждение пароля -->
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Повторите пароль</label>
              <div class="relative">
                <input v-model="confirmPassword" :type="showConfirmPassword ? 'text' : 'password'" required
                  :class="['w-full px-4 py-3 pr-12 rounded-xl bg-white dark:bg-slate-700 border focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white',
                    confirmPassword && password !== confirmPassword ? 'border-red-400' : 'border-slate-200 dark:border-slate-600']"
                  placeholder="••••••••">
                <button type="button" @click="showConfirmPassword = !showConfirmPassword"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors p-1">
                  <svg v-if="!showConfirmPassword" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                  <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                  </svg>
                </button>
              </div>
              <p v-if="confirmPassword && password !== confirmPassword" class="text-xs text-red-500 mt-1 ml-1">Пароли не совпадают</p>
            </div>

            <!-- Секретный вопрос -->
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Секретный вопрос</label>
              <input v-model="securityQuestion" type="text" required
                class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
                placeholder="Например: кличка первого питомца">
            </div>

            <!-- Ответ -->
            <div>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Ответ</label>
              <input v-model="securityAnswer" type="text" required
                class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
                placeholder="Ответ (регистр не важен)">
              <p class="text-xs text-slate-400 mt-1 ml-1">Запомни ответ — он нужен для восстановления пароля</p>
            </div>

          </div>
        </Transition>

        <button type="submit" :disabled="loading || (mode === 'register' && !isPasswordValid)"
          class="w-full bg-linear-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-bold py-3 rounded-xl shadow-lg active:scale-95 transition-all mt-2 disabled:opacity-50 disabled:cursor-not-allowed">
          <span v-if="loading">Секунду...</span>
          <span v-else>{{ mode === 'login' ? 'Войти' : 'Создать аккаунт' }}</span>
        </button>
      </form>

      <!-- ===== ВОССТАНОВЛЕНИЕ ===== -->
      <div v-if="mode === 'recover'" class="space-y-4">

        <!-- Шаг 1 -->
        <div v-if="recoverStep === 1" class="space-y-4">
          <p class="text-sm text-slate-600 dark:text-slate-400 text-center">Введи свой ник — мы покажем секретный вопрос</p>
          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Ник</label>
            <input v-model="recoverUsername" type="text"
              class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
              placeholder="Введи ник...">
          </div>
          <button @click="handleRecoverStep1" :disabled="loading"
            class="w-full bg-linear-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl shadow-lg active:scale-95 transition-all disabled:opacity-50">
            <span v-if="loading">Секунду...</span>
            <span v-else>Далее →</span>
          </button>
        </div>

        <!-- Шаг 2 -->
        <div v-if="recoverStep === 2" class="space-y-4">
          <div class="p-3 bg-purple-50 dark:bg-purple-900/20 border border-purple-200 dark:border-purple-700 rounded-xl">
            <p class="text-xs text-slate-500 dark:text-slate-400 mb-1">Секретный вопрос:</p>
            <p class="text-sm font-medium text-slate-800 dark:text-slate-200">{{ recoverQuestion }}</p>
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Ответ</label>
            <input v-model="recoverAnswer" type="text"
              class="w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
              placeholder="Твой ответ...">
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Новый пароль</label>
            <div class="relative">
              <input v-model="recoverNewPassword" :type="showRecoverPassword ? 'text' : 'password'"
                class="w-full px-4 py-3 pr-12 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white"
                placeholder="••••••••">
              <button type="button" @click="showRecoverPassword = !showRecoverPassword"
                class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 transition-colors p-1">
                <svg v-if="!showRecoverPassword" xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                </svg>
                <svg v-else xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                </svg>
              </button>
            </div>

            <!-- Полоска сложности для восстановления -->
            <Transition name="slide">
              <div v-if="recoverNewPassword.length > 0" class="mt-2 space-y-1">
                <div class="w-full bg-slate-200 dark:bg-slate-700 rounded-full h-1.5 overflow-hidden">
                  <div :class="['h-full rounded-full transition-all duration-300', recoverPasswordStrength?.color, recoverPasswordStrength?.width]"></div>
                </div>
                <p :class="['text-xs font-medium', recoverPasswordStrength?.textColor]">
                  {{ recoverPasswordStrength?.label }} · {{ recoverPasswordScore }} очков
                </p>
              </div>
            </Transition>
          </div>

          <div>
            <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Повторите пароль</label>
            <input v-model="recoverConfirmPassword" type="password"
              :class="['w-full px-4 py-3 rounded-xl bg-white dark:bg-slate-700 border focus:ring-2 focus:ring-purple-500 outline-none transition-all dark:text-white',
                recoverConfirmPassword && recoverNewPassword !== recoverConfirmPassword ? 'border-red-400' : 'border-slate-200 dark:border-slate-600']"
              placeholder="••••••••">
            <p v-if="recoverConfirmPassword && recoverNewPassword !== recoverConfirmPassword"
               class="text-xs text-red-500 mt-1 ml-1">Пароли не совпадают</p>
          </div>

          <button @click="handleRecoverStep2" :disabled="loading"
            class="w-full bg-linear-to-r from-purple-600 to-indigo-600 text-white font-bold py-3 rounded-xl shadow-lg active:scale-95 transition-all disabled:opacity-50">
            <span v-if="loading">Секунду...</span>
            <span v-else>Сменить пароль</span>
          </button>
        </div>
      </div>

      <div class="mt-6 flex flex-col items-center gap-2">
        <button v-if="mode !== 'recover'" @click="setMode(mode === 'login' ? 'register' : 'login')"
          class="text-purple-600 dark:text-purple-400 text-sm hover:underline font-medium">
          {{ mode === 'login' ? 'Нет аккаунта? Зарегистрироваться' : 'Уже есть аккаунт? Войти' }}
        </button>
        <button v-if="mode === 'login'" @click="setMode('recover')"
          class="text-slate-400 dark:text-slate-500 text-xs hover:underline">
          Забыл пароль
        </button>
        <button v-if="mode === 'recover'" @click="setMode('login')"
          class="text-slate-400 dark:text-slate-500 text-sm hover:underline">
          ← Назад к входу
        </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
.slide-enter-active, .slide-leave-active {
  transition: all 0.25s ease;
  overflow: hidden;
}
.slide-enter-from, .slide-leave-to {
  opacity: 0;
  max-height: 0;
}
.slide-enter-to, .slide-leave-from {
  opacity: 1;
  max-height: 800px;
}
</style>