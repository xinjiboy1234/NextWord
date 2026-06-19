interface ArticleTextProps {
  content: string
  onWordClick: (word: string, paragraphIndex: number, paragraphText: string) => void
}

function tokenizeParagraph(text: string) {
  return text.split(/(\s+|[.,!?;:"'()—-])/).filter(Boolean)
}

export function ArticleText({ content, onWordClick }: ArticleTextProps) {
  const paragraphs = content.split(/\n\s*\n/).filter(Boolean)

  return (
    <div className="space-y-4 text-base leading-7 text-neutral-800">
      {paragraphs.map((paragraph, index) => (
        <p key={index} className="rounded-md border border-transparent px-1 py-1 hover:border-neutral-200">
          {tokenizeParagraph(paragraph).map((token, tokenIndex) => {
            const isWord = /^[A-Za-z'-]+$/.test(token)
            if (!isWord) {
              return <span key={tokenIndex}>{token}</span>
            }

            return (
              <button
                key={tokenIndex}
                type="button"
                onClick={() => onWordClick(token, index, paragraph)}
                className="mx-0 inline rounded px-0.5 font-medium text-emerald-800 underline decoration-emerald-300 underline-offset-2 hover:bg-emerald-50"
              >
                {token}
              </button>
            )
          })}
        </p>
      ))}
    </div>
  )
}
