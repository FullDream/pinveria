import { type FieldTree } from '@angular/forms/signals'
import { type Game, type ImageAsset } from '@pinveria/games/data-access'

import { type GameForm } from '@pinveria/games/ui'

export type FeatureEditorContext = {
	fields: FieldTree<GameForm>
	cover: ImageAsset | undefined
	banner: ImageAsset | null | undefined
	maps: Game['maps']
	chooseBanner: () => void
	removeBanner: () => void
	editing: boolean
	busy: boolean
	deleting: boolean
	gameName: string
	chooseCover: () => void
	changeStatus: (value: unknown) => void
	deleteGame: () => Promise<void>
}
