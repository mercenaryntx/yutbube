import { Component, OnInit } from '@angular/core';
import { SignalRService } from './signalr.service';
import { MatSnackBar } from '@angular/material';

export interface IVideo {
	id: string;
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
  
  videos = new Map<string, IVideo>();

  constructor(signalRService: SignalRService, snackBar: MatSnackBar) {
    this._signalRService = signalRService;
    this._snackBar = snackBar;
  }  

  ngOnInit() {
    this._signalRService.init();
    this._signalRService.messages.subscribe((video:any) => {
	  if (video.error != null) {
		  this._snackBar.open(video.error);
	  } else {
		  if (video.isReady) this._snackBar.open(video.fileName + ' is downloadable now.');
	  }
	  this.videos.set(video.id, video);
    });
  }

  send() {
    this._signalRService.send(this.videoId).subscribe(() => {});
  }
}
