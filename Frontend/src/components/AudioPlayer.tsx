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
      className="grid h-10 w-10 place-items-center rounded-md border border-neutral-200 bg-white text-neutral-700 hover:bg-neutral-100"
    >
      <Volume2 size={18} aria-hidden="true" />
    </button>
  )
}
