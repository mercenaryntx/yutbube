import { Component, OnInit } from '@angular/core';
import { SignalRService } from './signalr.service';
import { MatSnackBar } from '@angular/material';
import { Router, ActivatedRoute, NavigationEnd } from '@angular/router';

export interface IVideo {
  id: string;
  title: string;
  duration: string;
  thumbnail: string;
  url: string;
  filename: string;
  isReady: boolean;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styles: []
})
export class AppComponent implements OnInit {

  videoId: string;
  extendedMode = false;

  videos = new Map<string, IVideo>();

  constructor(private signalRService: SignalRService, private snackBar: MatSnackBar, private router: Router, private route: ActivatedRoute) {}

  ngOnInit() {
    this.signalRService.init(() => {
      this.router.events.subscribe((e) => {
        if (e instanceof NavigationEnd) {
          const id = e.url.substring(2);
          if (id !== "") this.send(id);
        }
      });
      this.send(this.route.snapshot.fragment);
    });
    this.signalRService.messages.subscribe((video: any) => {
      if (!video.isEnqueued) {
        if (video.error != null) {
          this.snackBar.open(video.error);
        } else {
          if (video.isReady) this.snackBar.open(video.fileName + ' is downloadable now.');
        }
      }
      this.videos.set(video.id, video);
    });
  }

  send(id) {
    this.signalRService.send(id).subscribe(() => { });
  }
}
