import { booleanAttribute, Component, input } from '@angular/core'
import { NgIcon, provideIcons } from '@ng-icons/core'
import { matMapRound } from '@ng-icons/material-symbols/round'
import { type MediaImageFormat } from '@pinveria/shared/util'
import { MediaThumbnail, type PickerImage } from '../media-thumbnail/media-thumbnail'

@Component({
	selector: 'ui-image-preview',
	imports: [MediaThumbnail, NgIcon],
	templateUrl: './image-preview.html',
	providers: [provideIcons({ matMapRound })],
	host: { class: 'block overflow-hidden bg-muted' },
})
export class ImagePreview {
	readonly image = input<PickerImage | null>(null)
	readonly sizes = input<string>('100vw')
	readonly format = input<MediaImageFormat | null>(null)
	readonly priority = input(false, { transform: booleanAttribute })
}
