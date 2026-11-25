import { Injectable } from '@angular/core'
import { PageTitleStrategy } from '@pinveria/shared/ui'

@Injectable()
export class WebTitleStrategy extends PageTitleStrategy {
	protected readonly applicationTitle = 'Pinveria'
}
