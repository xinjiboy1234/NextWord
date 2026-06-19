import { ArrowLeft, BookOpenCheck } from 'lucide-react'
import { useEffect } from 'react'
import { ArticleText } from '../components/ArticleText'
import { CommentThread } from '../components/CommentThread'
import { VocabExtractPanel } from '../components/VocabExtractPanel'
import { WordPopover } from '../components/WordPopover'
import { useArticleReader } from '../hooks/useArticleReader'
import { useVocabExtract } from '../hooks/useVocabExtract'
import { useWordLookup } from '../hooks/useWordLookup'

interface ArticleReaderProps {
  articleId: string
  onBack: () => void
}

export function ArticleReader({ articleId, onBack }: ArticleReaderProps) {
  const reader = useArticleReader(articleId)
  const lookup = useWordLookup(articleId)
  const vocab = useVocabExtract(articleId)

  useEffect(() => {
    void vocab.loadExisting()
  }, [articleId])

  async function handleWordClick(word: string, paragraphIndex: number, paragraphText: string) {
    await lookup.lookup(word, paragraphText)
    await reader.recordLookup()
    // 方便评论时引用段落
    void paragraphIndex
  }

  if (reader.loading) {
    return <p className="text-sm text-neutral-600">加载文章中...</p>
  }

  if (reader.error || !reader.article) {
    return (
      <div className="rounded-md border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
        {reader.error ?? '文章不存在。'}
      </div>
    )
  }

  return (
    <div className="grid gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <button
          type="button"
          onClick={onBack}
          className="inline-flex h-10 items-center gap-2 rounded-md border border-neutral-200 px-3 text-sm"
        >
          <ArrowLeft size={16} />
          返回文库
        </button>
        <button
          type="button"
          onClick={() => void reader.finishReading(reader.comments.length)}
          className="inline-flex h-10 items-center gap-2 rounded-md bg-emerald-700 px-3 text-sm font-medium text-white"
        >
          <BookOpenCheck size={16} />
          完成阅读
        </button>
      </div>

      <section className="rounded-md border border-neutral-200 bg-white p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-2xl font-semibold">{reader.article.title}</h2>
            <p className="mt-1 text-sm text-neutral-600">
              {reader.article.difficultyLevel} / {reader.article.cefrLevel} · {reader.article.wordCount} 词
              {reader.article.topicTag ? ` · ${reader.article.topicTag}` : ''}
            </p>
          </div>
          <p className="text-sm text-neutral-600">查词 {reader.lookupCount} 次</p>
        </div>

        <div className="mt-5 grid gap-4 lg:grid-cols-[1fr_280px]">
          <ArticleText content={reader.article.content} onWordClick={(word, index, text) => void handleWordClick(word, index, text)} />
          <WordPopover
            word={lookup.selectedWord}
            definition={lookup.definition}
            loading={lookup.loading}
            onClose={lookup.clear}
          />
        </div>
      </section>

      <VocabExtractPanel
        items={vocab.items}
        loading={vocab.loading}
        error={vocab.error}
        onExtract={() => void vocab.extract()}
        onWordSelect={(word) => void lookup.lookup(word)}
      />

      <CommentThread
        articleId={articleId}
        comments={reader.comments}
        onAdded={() => void reader.refreshComments()}
      />
    </div>
  )
}
