import { HlmCarouselImports } from '@spartan-ng/helm/carousel'
import { HlmCheckbox } from '@spartan-ng/helm/checkbox'
import { HlmLabel } from '@spartan-ng/helm/label'
import { NgTemplateOutlet } from '@angular/common'
import { HlmAspectRatioImports } from '@spartan-ng/helm/aspect-ratio'
import { Button } from '@pinveria/shared/ui'
import { HlmTooltip } from '@spartan-ng/helm/tooltip'
import { Component, computed, input, output } from '@angular/core'
import type { PublicCategory, PublicMarker } from '@pinveria/maps/data-access'
import { CategoryIcon } from '@pinveria/maps/ui'
import { provideIcons } from '@ng-icons/core'
import { matCloseRound, matOpenInNewRound } from '@ng-icons/material-symbols/round'

@Component({
	selector: 'maps-marker-popup',
	imports: [
		Button,
		CategoryIcon,
		HlmTooltip,
		HlmCheckbox,
		HlmLabel,
		NgTemplateOutlet,
		HlmCarouselImports,
		HlmAspectRatioImports,
	],
	templateUrl: './marker-popup.html',
	providers: [
		provideIcons({
			matCloseRound,
			matOpenInNewRound,
		}),
	],
	host: {
		class: 'flex min-h-0 flex-col',
	},
})
export class MarkerPopup {
	readonly panel = input(false)
	readonly mobile = input(false)
	readonly contentOnly = input(false)
	readonly marker = input<PublicMarker | null>(null)
	readonly category = input<PublicCategory | null>(null)
	readonly isCompleted = input<boolean>(false)

	readonly closed = output<void>()
	readonly expand = output<void>()
	readonly toggleCompleted = output<void>()

	protected readonly hasContent = computed(() => {
		const marker = this.marker()

		return !!marker && (marker.media.length > 0 || !!marker.description?.trim())
	})
}
