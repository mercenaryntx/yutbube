import { Component, OnInit } from '@angular/core';
import { SignalRService } from './signalr.service';
import { MatSnackBar } from '@angular/material';
import { HttpParams } from '@angular/common/http';

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

  private readonly _signalRService: SignalRService;
  private readonly _snackBar: MatSnackBar;
  videoId: string;
  extendedMode = false;

  videos = new Map<string, IVideo>();

  constructor(signalRService: SignalRService, snackBar: MatSnackBar) {
    this._signalRService = signalRService;
    this._snackBar = snackBar;
  }

  ngOnInit() {
    this._signalRService.init();
    this._signalRService.messages.subscribe((video: any) => {
      if (video.error != null) {
        this._snackBar.open(video.error);
      } else {
        if (video.isReady) this._snackBar.open(video.fileName + ' is downloadable now.');
      }
      this.videos.set(video.id, video);
    });
    this.videoId = this.getParamValueQueryString('v');
    if (this.videoId != null) this._signalRService.send(this.videoId).subscribe(() => { });
  }

  getParamValueQueryString(paramName) {
    const url = window.location.href;
    let paramValue;
    if (url.includes('?')) {
      const httpParams = new HttpParams({ fromString: url.split('?')[1] });
      paramValue = httpParams.get(paramName);
    }
    return paramValue;
  }

  send() {
    this._signalRService.send(this.videoId).subscribe(() => { });
  }
}
