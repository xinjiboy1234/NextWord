import { Volume2 } from 'lucide-react'

interface AudioPlayerProps {
  text: string
}

export function AudioPlayer({ text }: AudioPlayerProps) {
  function speak() {
    if (!('speechSynthesis' in window)) return
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = 'en-US'
    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(utterance)
  }

  return (
    <button
      type="button"
      onClick={speak}
      title="播放发音"
      className="btn btn-icon btn-secondary"
      aria-label="播放发音"
    >
      <Volume2 size={18} aria-hidden="true" />
    </button>
  )
}
