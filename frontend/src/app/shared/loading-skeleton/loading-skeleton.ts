import { Component, input } from '@angular/core';

/** A stack of shimmering placeholder rows — for list/panel content still loading. */
@Component({
  selector: 'app-loading-skeleton',
  styleUrl: './loading-skeleton.scss',
  templateUrl: './loading-skeleton.html'
})
export class LoadingSkeleton {
  readonly rows = input(3);
  readonly height = input('56px');

  get rowsArray(): number[] {
    return Array.from({ length: this.rows() }, (_, i) => i);
  }
}
