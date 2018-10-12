import { Component, OnInit } from '@angular/core';
import { SignalRService } from './signalr.service';
import { MatSnackBar } from '@angular/material';
import { Router, ActivatedRoute, NavigationEnd } from '@angular/router';
import { MatTabChangeEvent } from '@angular/material/tabs';
import { environment } from '../environments/environment';
import { HttpClient } from "@angular/common/http";
import * as _ from 'lodash';

export interface IVideo {
  id: string;
  title: string;
  duration: string;
  thumbnail: string;
  url: string;
  fileName: string;
  isReady: boolean;
  message: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styles: []
})
export class AppComponent implements OnInit {

  private readonly _baseUrl: string = environment.baseUrl;
  private readonly _apiKey: string = environment.apiKey;
  public readonly clientVersion: string = environment.clientVersion;
  functionVersion: string;
  videoId: string;
  extendedMode = false;

  queue = new Map<string, IVideo>();
  history: IVideo[];

  tabChanged = (e: MatTabChangeEvent): void => {
    if (e.index === 1) this.list();
  }

  constructor(private signalRService: SignalRService, private snackBar: MatSnackBar, private router: Router, private route: ActivatedRoute, private http: HttpClient) {}

  ngOnInit() {
    this.signalRService.init(() => {
      this.router.events.subscribe((e) => {
        if (e instanceof NavigationEnd) {
          const id = e.url.substring(2);
          if (id !== "") this.send(id);
        }
      });
      if (this.route.snapshot.fragment != null && this.route.snapshot.fragment !== "") {
        this.send(this.route.snapshot.fragment);
      }
      this.http.get<string>(`${this._baseUrl}version/?code=${this._apiKey}`).subscribe(res => {
        this.functionVersion = res;
      });
    });

    this.signalRService.messages.subscribe((video: any) => {
      //console.table(video);
      if (!video.isEnqueued) {
        if (video.error != null) {
          this.snackBar.open(video.error);
        } else {
          if (video.isReady) this.snackBar.open(video.fileName + ' is downloadable now.');
        }
      }
      this.queue.set(video.id, video);
    });
  }

  navigate() {
    this.signalRService.send(this.videoId).subscribe(() => { });
    //this.router.navigateByUrl(`#${this.videoId}`);
  }

  send(id) {
    this.signalRService.send(id).subscribe(() => { });
  }

  list() {
    this.http.get<IVideo[]>(`${this._baseUrl}list/?code=${this._apiKey}`).subscribe(res => {
      this.history = res;
    });
  }
}
