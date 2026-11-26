<script setup lang="ts">
import { ref, computed } from 'vue';
import { useAuthStore } from '../store/auth.ts';
import { useRouter } from 'vue-router';
import { useTheme } from '../composables/useTheme';
import axios from 'axios';
import {
  Eye,
  EyeOff,
  Sun,
  Moon,
  ArrowLeft,
  ArrowRight,
  Check,
  AlertCircle,
  CheckCircle2,
  Loader2
} from 'lucide-vue-next';

const auth = useAuthStore();
const router = useRouter();
const { isDark, toggleTheme } = useTheme();

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
    else score += 3;
  }
  return score;
};

const getStrengthMeta = (pwd: string, score: number) => {
  const len = pwd.length;
  if (len === 0) return null;
  if (len < 8 || score < 8) return { label: 'Очень слабый', level: 1, color: 'bg-red-500', textColor: 'text-red-500 dark:text-red-400' };
  if (score < 12) return { label: 'Слабый', level: 2, color: 'bg-orange-500', textColor: 'text-orange-500 dark:text-orange-400' };
  if (score < 15) return { label: 'Средний', level: 3, color: 'bg-amber-500', textColor: 'text-amber-500 dark:text-amber-400' };
  if (score < 22) return { label: 'Надёжный', level: 4, color: 'bg-emerald-500', textColor: 'text-emerald-600 dark:text-emerald-400' };
  return { label: 'Отличный', level: 4, color: 'bg-emerald-500', textColor: 'text-emerald-600 dark:text-emerald-400' };
};

const passwordScore = computed(() => calcPasswordScore(password.value));
const passwordStrength = computed(() => getStrengthMeta(password.value, passwordScore.value));
const isPasswordValid = computed(() => password.value.length >= 8 && passwordScore.value >= 15);

const recoverPasswordScore = computed(() => calcPasswordScore(recoverNewPassword.value));
const recoverPasswordStrength = computed(() => getStrengthMeta(recoverNewPassword.value, recoverPasswordScore.value));

const handleSubmit = async () => {
  error.value = '';
  successMessage.value = '';

  if (mode.value === 'register') {
    const trimmedUsername = username.value.trim();
    if (trimmedUsername.length < 4 || trimmedUsername.length > 32) {
      error.value = 'Никнейм должен содержать от 4 до 32 символов.';
      return;
    }
    if (password.value !== confirmPassword.value) {
      error.value = 'Пароли не совпадают.';
      return;
    }
    if (!isPasswordValid.value) {
      error.value = 'Пароль слишком простой. Добавьте заглавные буквы, цифры или спецсимволы.';
      return;
    }
    if (!securityQuestion.value.trim() || !securityAnswer.value.trim()) {
      error.value = 'Укажите секретный вопрос и ответ.';
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
      successMessage.value = 'Аккаунт создан. Теперь вы можете войти.';
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
    error.value = 'Введите никнейм.';
    return;
  }
  loading.value = true;
  try {
    const res = await axios.get(`/api/auth/question/${recoverUsername.value.trim()}`);
    recoverQuestion.value = res.data.question;
    recoverStep.value = 2;
  } catch (e: any) {
    error.value = e.response?.data || 'Пользователь не найден.';
  } finally {
    loading.value = false;
  }
};

const handleRecoverStep2 = async () => {
  error.value = '';
  if (recoverNewPassword.value !== recoverConfirmPassword.value) {
    error.value = 'Пароли не совпадают.';
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
    successMessage.value = 'Пароль успешно изменён. Войдите с новым паролем.';
    setMode('login');
  } catch (e: any) {
    error.value = e.response?.data || 'Неверный ответ на секретный вопрос.';
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
  <div class="min-h-screen grid lg:grid-cols-2 bg-[#F7F8FA] dark:bg-[#0E1015] text-slate-900 dark:text-slate-100 transition-colors duration-200">

    <!-- ===== ЛЕВАЯ ПАНЕЛЬ ===== -->
    <aside class="hidden lg:flex flex-col justify-center items-center p-12 border-r border-slate-200/80 dark:border-white/[0.06] bg-white dark:bg-[#12151C]">
      <div class="flex flex-col items-center text-center max-w-xs">
        <div class="w-16 h-16 mb-6 text-blue-600 dark:text-blue-500">
          <svg viewBox="0 0 48 48" fill="none" class="w-full h-full">
            <rect x="2" y="2" width="44" height="44" rx="12" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-opacity="0.22" stroke-width="1.5" />
            <ellipse cx="24" cy="24" rx="14" ry="6.5" transform="rotate(-40 24 24)" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
            <ellipse cx="24" cy="24" rx="14" ry="6.5" transform="rotate(40 24 24)" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-opacity="0.5" />
            <circle cx="24" cy="24" r="3.5" fill="currentColor" />
            <circle cx="34.5" cy="15.5" r="2" fill="currentColor" />
          </svg>
        </div>

        <h1 class="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">
          Pozitron
        </h1>
        <p class="mt-2.5 text-sm text-slate-500 dark:text-slate-400 leading-relaxed">
          Быстрый и приватный мессенджер для ежедневного общения.
        </p>
      </div>
    </aside>

    <!-- ===== ПРАВАЯ ПАНЕЛЬ (ФОРМА) ===== -->
    <main class="relative flex flex-col justify-center items-center p-6 sm:p-12">
      <!-- Переключатель темы -->
      <button
        type="button"
        @click="toggleTheme"
        :title="isDark ? 'Включить светлую тему' : 'Включить тёмную тему'"
        class="absolute top-6 right-6 w-9 h-9 rounded-xl border border-slate-200 dark:border-white/[0.08] bg-white dark:bg-[#161922] hover:bg-slate-100 dark:hover:bg-[#1D212D] text-slate-500 dark:text-slate-400 flex items-center justify-center transition-colors"
      >
        <Sun v-if="isDark" class="w-4 h-4" />
        <Moon v-else class="w-4 h-4" />
      </button>

      <div class="w-full max-w-[380px]">
        <!-- Мобильный логотип -->
        <div class="lg:hidden flex items-center gap-3 mb-8 select-none">
          <div class="w-9 h-9 text-blue-600 dark:text-blue-500">
            <svg viewBox="0 0 48 48" fill="none" class="w-full h-full">
              <rect x="2" y="2" width="44" height="44" rx="12" fill="currentColor" fill-opacity="0.1" stroke="currentColor" stroke-opacity="0.22" stroke-width="1.5" />
              <ellipse cx="24" cy="24" rx="14" ry="6.5" transform="rotate(-40 24 24)" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
              <ellipse cx="24" cy="24" rx="14" ry="6.5" transform="rotate(40 24 24)" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-opacity="0.5" />
              <circle cx="24" cy="24" r="3.5" fill="currentColor" />
              <circle cx="34.5" cy="15.5" r="2" fill="currentColor" />
            </svg>
          </div>
          <span class="text-lg font-semibold tracking-tight text-slate-900 dark:text-white">Pozitron</span>
        </div>

        <!-- Заголовок -->
        <div class="mb-7">
          <h2 class="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">
            <template v-if="mode === 'login'">Вход в аккаунт</template>
            <template v-else-if="mode === 'register'">Создать аккаунт</template>
            <template v-else>Восстановление пароля</template>
          </h2>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">
            <template v-if="mode === 'login'">Рады видеть вас снова.</template>
            <template v-else-if="mode === 'register'">Заполните поля ниже, чтобы начать.</template>
            <template v-else-if="recoverStep === 1">Введите никнейм для получения секретного вопроса.</template>
            <template v-else>Ответьте на вопрос и задайте новый пароль.</template>
          </p>
        </div>

        <!-- Уведомление об успехе -->
        <div v-if="successMessage"
          class="mb-5 p-3.5 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-700 dark:text-emerald-300 text-xs flex items-start gap-2.5">
          <CheckCircle2 class="w-4 h-4 shrink-0 mt-0.5" />
          <span>{{ successMessage }}</span>
        </div>

        <!-- Уведомление об ошибке -->
        <div v-if="error"
          class="mb-5 p-3.5 rounded-xl bg-red-500/10 border border-red-500/20 text-red-700 dark:text-red-300 text-xs flex items-start gap-2.5">
          <AlertCircle class="w-4 h-4 shrink-0 mt-0.5" />
          <span>{{ error }}</span>
        </div>

        <!-- ===== ВХОД / РЕГИСТРАЦИЯ ===== -->
        <form v-if="mode !== 'recover'" @submit.prevent="handleSubmit" class="space-y-4">

          <!-- Никнейм -->
          <div>
            <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Никнейм</label>
            <input
              v-model="username"
              type="text"
              required
              autocomplete="username"
              class="w-full px-3.5 py-2.5 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
              placeholder="Введите никнейм"
            >
            <p v-if="mode === 'register' && username.trim().length > 0 && username.trim().length < 4"
               class="text-xs text-red-500 dark:text-red-400 mt-1.5">
              Минимум 4 символа
            </p>
          </div>

          <!-- Пароль -->
          <div>
            <div class="flex items-center justify-between mb-1.5">
              <label class="block text-xs font-medium text-slate-600 dark:text-slate-300">Пароль</label>
              <button
                v-if="mode === 'login'"
                type="button"
                @click="setMode('recover')"
                class="text-xs text-blue-600 dark:text-blue-400 hover:underline"
              >
                Забыли пароль?
              </button>
            </div>
            <div class="relative">
              <input
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                required
                :autocomplete="mode === 'login' ? 'current-password' : 'new-password'"
                class="w-full px-3.5 py-2.5 pr-10 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
                placeholder="••••••••"
              >
              <button
                type="button"
                @click="showPassword = !showPassword"
                class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 transition-colors p-1"
              >
                <EyeOff v-if="showPassword" class="w-4 h-4" />
                <Eye v-else class="w-4 h-4" />
              </button>
            </div>

            <!-- Сложность пароля при регистрации -->
            <Transition name="slide">
              <div v-if="mode === 'register' && password.length > 0" class="mt-2.5 space-y-2">
                <div class="grid grid-cols-4 gap-1.5">
                  <div v-for="i in 4" :key="i"
                    :class="[
                      'h-1 rounded-full transition-colors duration-300',
                      (passwordStrength?.level ?? 0) >= i
                        ? passwordStrength?.color
                        : 'bg-slate-200 dark:bg-white/[0.08]'
                    ]"
                  ></div>
                </div>
                <div class="flex items-center justify-between text-xs">
                  <span :class="['font-medium', passwordStrength?.textColor]">
                    {{ passwordStrength?.label }}
                  </span>
                  <span class="font-mono text-[11px] text-slate-400 dark:text-slate-500">
                    {{ passwordScore }} / 15+
                  </span>
                </div>
                <div class="text-xs text-slate-500 dark:text-slate-400 space-y-1">
                  <p :class="['flex items-center gap-1.5', password.length >= 8 ? 'text-emerald-600 dark:text-emerald-400' : '']">
                    <Check v-if="password.length >= 8" class="w-3.5 h-3.5" />
                    <span v-else class="w-3.5 text-center">·</span>
                    <span>Минимум 8 символов</span>
                  </p>
                  <p :class="['flex items-center gap-1.5', passwordScore >= 15 ? 'text-emerald-600 dark:text-emerald-400' : '']">
                    <Check v-if="passwordScore >= 15" class="w-3.5 h-3.5" />
                    <span v-else class="w-3.5 text-center">·</span>
                    <span>Достаточная сложность (от 15 баллов)</span>
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
                <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Повторите пароль</label>
                <div class="relative">
                  <input
                    v-model="confirmPassword"
                    :type="showConfirmPassword ? 'text' : 'password'"
                    required
                    autocomplete="new-password"
                    :class="[
                      'w-full px-3.5 py-2.5 pr-10 rounded-xl bg-white dark:bg-[#161922] border outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500',
                      confirmPassword && password !== confirmPassword
                        ? 'border-red-500/70 focus:border-red-500 focus:ring-2 focus:ring-red-500/15'
                        : 'border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15'
                    ]"
                    placeholder="••••••••"
                  >
                  <button
                    type="button"
                    @click="showConfirmPassword = !showConfirmPassword"
                    class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 transition-colors p-1"
                  >
                    <EyeOff v-if="showConfirmPassword" class="w-4 h-4" />
                    <Eye v-else class="w-4 h-4" />
                  </button>
                </div>
                <p v-if="confirmPassword && password !== confirmPassword" class="text-xs text-red-500 dark:text-red-400 mt-1.5">
                  Пароли не совпадают
                </p>
              </div>

              <!-- Секретный вопрос -->
              <div>
                <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Секретный вопрос</label>
                <input
                  v-model="securityQuestion"
                  type="text"
                  required
                  class="w-full px-3.5 py-2.5 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
                  placeholder="Например: кличка первого питомца"
                >
              </div>

              <!-- Ответ -->
              <div>
                <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Ответ</label>
                <input
                  v-model="securityAnswer"
                  type="text"
                  required
                  class="w-full px-3.5 py-2.5 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
                  placeholder="Ответ (регистр не важен)"
                >
              </div>

            </div>
          </Transition>

          <button
            type="submit"
            :disabled="loading || (mode === 'register' && !isPasswordValid)"
            class="w-full bg-blue-600 hover:bg-blue-700 dark:bg-blue-600 dark:hover:bg-blue-500 text-white font-medium py-2.5 rounded-xl text-sm transition-colors mt-2 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center gap-2"
          >
            <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
            <span>{{ loading ? 'Секунду...' : (mode === 'login' ? 'Войти' : 'Создать аккаунт') }}</span>
          </button>
        </form>

        <!-- ===== ВОССТАНОВЛЕНИЕ ПАРОЛЯ ===== -->
        <div v-if="mode === 'recover'" class="space-y-4">

          <!-- Шаг 1 -->
          <div v-if="recoverStep === 1" class="space-y-4">
            <div>
              <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Никнейм</label>
              <input
                v-model="recoverUsername"
                type="text"
                @keyup.enter="handleRecoverStep1"
                class="w-full px-3.5 py-2.5 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
                placeholder="Введите никнейм"
              >
            </div>
            <button
              type="button"
              @click="handleRecoverStep1"
              :disabled="loading"
              class="w-full bg-blue-600 hover:bg-blue-700 dark:bg-blue-600 dark:hover:bg-blue-500 text-white font-medium py-2.5 rounded-xl text-sm transition-colors disabled:opacity-40 flex items-center justify-center gap-2"
            >
              <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
              <template v-else>
                <span>Далее</span>
                <ArrowRight class="w-4 h-4" />
              </template>
            </button>
          </div>

          <!-- Шаг 2 -->
          <div v-if="recoverStep === 2" class="space-y-4">
            <div class="p-3.5 rounded-xl bg-slate-100 dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08]">
              <p class="text-[11px] font-medium text-slate-400 dark:text-slate-500 mb-1">
                Секретный вопрос
              </p>
              <p class="text-sm font-medium text-slate-900 dark:text-white">{{ recoverQuestion }}</p>
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Ответ</label>
              <input
                v-model="recoverAnswer"
                type="text"
                class="w-full px-3.5 py-2.5 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
                placeholder="Ваш ответ"
              >
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Новый пароль</label>
              <div class="relative">
                <input
                  v-model="recoverNewPassword"
                  :type="showRecoverPassword ? 'text' : 'password'"
                  class="w-full px-3.5 py-2.5 pr-10 rounded-xl bg-white dark:bg-[#161922] border border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15 outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500"
                  placeholder="••••••••"
                >
                <button
                  type="button"
                  @click="showRecoverPassword = !showRecoverPassword"
                  class="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 transition-colors p-1"
                >
                  <EyeOff v-if="showRecoverPassword" class="w-4 h-4" />
                  <Eye v-else class="w-4 h-4" />
                </button>
              </div>

              <!-- Сложность нового пароля -->
              <Transition name="slide">
                <div v-if="recoverNewPassword.length > 0" class="mt-2.5 space-y-1.5">
                  <div class="grid grid-cols-4 gap-1.5">
                    <div v-for="i in 4" :key="i"
                      :class="[
                        'h-1 rounded-full transition-colors duration-300',
                        (recoverPasswordStrength?.level ?? 0) >= i
                          ? recoverPasswordStrength?.color
                          : 'bg-slate-200 dark:bg-white/[0.08]'
                      ]"
                    ></div>
                  </div>
                  <div class="flex items-center justify-between text-xs">
                    <span :class="['font-medium', recoverPasswordStrength?.textColor]">
                      {{ recoverPasswordStrength?.label }}
                    </span>
                    <span class="font-mono text-[11px] text-slate-400 dark:text-slate-500">
                      {{ recoverPasswordScore }} / 15+
                    </span>
                  </div>
                </div>
              </Transition>
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1.5">Повторите пароль</label>
              <input
                v-model="recoverConfirmPassword"
                type="password"
                :class="[
                  'w-full px-3.5 py-2.5 rounded-xl bg-white dark:bg-[#161922] border outline-none transition-all text-sm text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-slate-500',
                  recoverConfirmPassword && recoverNewPassword !== recoverConfirmPassword
                    ? 'border-red-500/70 focus:border-red-500 focus:ring-2 focus:ring-red-500/15'
                    : 'border-slate-200 dark:border-white/[0.08] focus:border-blue-600 dark:focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15'
                ]"
                placeholder="••••••••"
              >
              <p v-if="recoverConfirmPassword && recoverNewPassword !== recoverConfirmPassword"
                 class="text-xs text-red-500 dark:text-red-400 mt-1.5">
                Пароли не совпадают
              </p>
            </div>

            <button
              type="button"
              @click="handleRecoverStep2"
              :disabled="loading"
              class="w-full bg-blue-600 hover:bg-blue-700 dark:bg-blue-600 dark:hover:bg-blue-500 text-white font-medium py-2.5 rounded-xl text-sm transition-colors disabled:opacity-40 flex items-center justify-center gap-2"
            >
              <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
              <span>{{ loading ? 'Секунду...' : 'Сменить пароль' }}</span>
            </button>
          </div>
        </div>

        <!-- Переключатель режима внизу -->
        <div class="mt-6 pt-5 border-t border-slate-200 dark:border-white/[0.06] flex items-center justify-center text-xs">
          <template v-if="mode === 'login'">
            <span class="text-slate-500 dark:text-slate-400">Нет аккаунта?&nbsp;</span>
            <button
              type="button"
              @click="setMode('register')"
              class="font-medium text-blue-600 dark:text-blue-400 hover:underline"
            >
              Зарегистрироваться
            </button>
          </template>
          <template v-else-if="mode === 'register'">
            <span class="text-slate-500 dark:text-slate-400">Уже есть аккаунт?&nbsp;</span>
            <button
              type="button"
              @click="setMode('login')"
              class="font-medium text-blue-600 dark:text-blue-400 hover:underline"
            >
              Войти
            </button>
          </template>
          <template v-else>
            <button
              type="button"
              @click="setMode('login')"
              class="font-medium text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white flex items-center gap-1.5 transition-colors"
            >
              <ArrowLeft class="w-3.5 h-3.5" />
              <span>Вернуться ко входу</span>
            </button>
          </template>
        </div>

      </div>
    </main>
  </div>
</template>

<style scoped>
.slide-enter-active, .slide-leave-active {
  transition: all 0.22s ease;
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
