import { inject, Injectable, signal } from '@angular/core'
import { Title } from '@angular/platform-browser'
import { RouterStateSnapshot, TitleStrategy } from '@angular/router'

@Injectable()
export abstract class PageTitleStrategy extends TitleStrategy {
	readonly #title = inject(Title)
	readonly #currentTitle = signal(this.#title.getTitle())

	readonly title = this.#currentTitle.asReadonly()

	protected abstract readonly applicationTitle: string

	override updateTitle(snapshot: RouterStateSnapshot): void {
		this.setPageTitle(this.buildTitle(snapshot))
	}

	setPageTitle(pageTitle?: string): void {
		const formatted = pageTitle ? `${pageTitle} | ${this.applicationTitle}` : this.applicationTitle

		this.#title.setTitle(formatted)
		this.#currentTitle.set(formatted)
	}
}
