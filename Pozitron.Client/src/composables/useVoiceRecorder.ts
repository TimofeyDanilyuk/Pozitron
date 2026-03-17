import { ref } from 'vue';

export function useVoiceRecorder(onRecorded: (file: File) => Promise<void>) {
  const isRecording = ref(false);
  let mediaRecorder: MediaRecorder | null = null;
  let audioChunks: Blob[] = [];

  const toggleRecording = async () => {
    if (isRecording.value) {
      mediaRecorder?.stop();
      isRecording.value = false;
    } else {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        audioChunks = [];

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
          if (audioChunks.length === 0) return;
          const blob = new Blob(audioChunks, { type: mimeType });
          if (blob.size < 1000) return;
          const ext = mimeType.includes('ogg') ? 'ogg' : 'webm';
          const file = new File([blob], `voice_${Date.now()}.${ext}`, { type: mimeType });
          await onRecorded(file);
        };

        mediaRecorder.start(100);
        isRecording.value = true;
      } catch {
        alert('Нет доступа к микрофону');
      }
    }
  };

  return { isRecording, toggleRecording };
}