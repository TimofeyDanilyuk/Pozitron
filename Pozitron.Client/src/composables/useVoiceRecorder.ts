import { ref } from 'vue';

export function useVoiceRecorder(onRecorded: (file: File) => Promise<void>) {
  const isRecording = ref(false);
  const recordingDuration = ref(0);
  let mediaRecorder: MediaRecorder | null = null;
  let audioChunks: Blob[] = [];
  let isCancelled = false;
  let timerInterval: ReturnType<typeof setInterval> | null = null;

  const startTimer = () => {
    recordingDuration.value = 0;
    timerInterval = setInterval(() => {
      recordingDuration.value++;
    }, 1000);
  };

  const stopTimer = () => {
    if (timerInterval) {
      clearInterval(timerInterval);
      timerInterval = null;
    }
  };

  const cancelRecording = () => {
    if (!isRecording.value) return;
    isCancelled = true;
    stopTimer();
    mediaRecorder?.stop();
    isRecording.value = false;
  };

  const toggleRecording = async () => {
    if (isRecording.value) {
      isCancelled = false;
      stopTimer();
      mediaRecorder?.stop();
      isRecording.value = false;
    } else {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        audioChunks = [];
        isCancelled = false;

        const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
          ? 'audio/webm;codecs=opus'
          : MediaRecorder.isTypeSupported('audio/webm')
            ? 'audio/webm'
            : 'audio/ogg';

        mediaRecorder = new MediaRecorder(stream, { mimeType });

        mediaRecorder.ondataavailable = (e) => {
          if (e.data.size > 0) audioChunks.push(e.data);
        };

        mediaRecorder.onstop = async () => {
          stream.getTracks().forEach(t => t.stop());
          stopTimer();
          if (isCancelled) {
            audioChunks = [];
            return;
          }
          if (audioChunks.length === 0) return;
          const blob = new Blob(audioChunks, { type: mimeType });
          if (blob.size < 1000) return;
          const ext = mimeType.includes('ogg') ? 'ogg' : 'webm';
          const file = new File([blob], `voice_${Date.now()}.${ext}`, { type: mimeType });
          await onRecorded(file);
        };

        mediaRecorder.start(100);
        isRecording.value = true;
        startTimer();
      } catch {
        alert('Нет доступа к микрофону');
      }
    }
  };

  return { isRecording, recordingDuration, toggleRecording, cancelRecording };
}
