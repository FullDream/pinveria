import { Component, input } from '@angular/core'
import { classes } from '@spartan-ng/helm/utils'

export type BrandLogoVariant = 'minimal' | 'medium' | 'full'

@Component({
	selector: 'ui-brand-logo',
	templateUrl: './brand-logo.html',
	host: {
		'aria-hidden': 'true',
		'data-slot': 'brand-logo',
		'[attr.data-variant]': 'variant()',
	},
})
export class BrandLogo {
	readonly variant = input<BrandLogoVariant>('medium')

	constructor() {
		classes(() => 'size-8 text-foreground flex shrink-0 items-center justify-center')
	}
}
