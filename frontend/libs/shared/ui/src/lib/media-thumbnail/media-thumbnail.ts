import { NgOptimizedImage } from '@angular/common'
import { booleanAttribute, Component, computed, inject, input, linkedSignal, signal } from '@angular/core'

import { NgIcon, provideIcons } from '@ng-icons/core'
import { matBrokenImageRound } from '@ng-icons/material-symbols/round'
import { MEDIA_IMAGE_SRCSET, type MediaImageFormat } from '@pinveria/shared/util'
import { MEDIA_IMAGE_OPTIMIZATION_ENABLED } from '../media-image-loader/provide-media-image-optimization'

export type PickerImage = { id: string; name: string; url: string; alt: string; placeholder: string | null }

@Component({
	selector: 'ui-media-thumbnail',
	imports: [NgOptimizedImage, NgIcon],
	templateUrl: './media-thumbnail.html',
	providers: [provideIcons({ matBrokenImageRound })],
	host: {
		class: 'relative block overflow-hidden bg-muted',
		'[attr.aria-label]': 'unavailable() ? "Image unavailable" : null',
	},
})
export class MediaThumbnail {
	readonly image = input.required<PickerImage>()
	readonly fit = input<'contain' | 'cover'>('contain')
	readonly sizes = input<string>('100vw')
	readonly format = input<MediaImageFormat | null>(null)
	readonly priority = input(false, { transform: booleanAttribute })

	protected readonly optimizationEnabled = inject(MEDIA_IMAGE_OPTIMIZATION_ENABLED)
	protected readonly srcset = MEDIA_IMAGE_SRCSET
	protected readonly failedUrl = signal<string | null>(null)
	protected readonly unavailable = computed(() => this.failedUrl() === this.image().url)
	protected readonly initialPriority = linkedSignal<boolean, boolean>({
		source: this.priority,
		computation: (value, previous) => previous?.value ?? value,
	})

	protected readonly loaderParams = computed<Record<string, unknown> | undefined>(() => {
		const format = this.format()

		return format ? { format } : undefined
	})
}
