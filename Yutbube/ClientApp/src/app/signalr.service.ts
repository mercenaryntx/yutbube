import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { HubConnection } from '@aspnet/signalr';
import * as signalR from '@aspnet/signalr';
import { Observable } from "rxjs";
import { SignalRConnectionInfo } from "./signalr-connection-info.model";
import { map } from "rxjs/operators";
import { Subject } from "rxjs";
import { environment } from '../environments/environment';
import { Guid } from "guid-typescript";

@Injectable()
export class SignalRService {

  private readonly _clientId: Guid = Guid.create();
  private readonly _http: HttpClient;
  private readonly _baseUrl: string = environment.baseUrl;
  private readonly _apiKey: string = environment.apiKey;
  private hubConnection: HubConnection;
  messages: Subject<string> = new Subject();

  constructor(http: HttpClient) {
    this._http = http;
  }

  private getConnectionInfo(): Observable<SignalRConnectionInfo> {
    let requestUrl = `${this._baseUrl}negotiate`;
    return this._http.get<SignalRConnectionInfo>(requestUrl);
  }

  init(callback) {
    console.log(`initializing SignalRService...`);
    this.getConnectionInfo().subscribe(info => {
      console.log(`received info for endpoint ${info.url}`);
      let options = {
        accessTokenFactory: () => info.accessToken
      };

      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(info.url, options)
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.hubConnection.start().catch(err => console.error(err.toString()));

      this.hubConnection.on(this._clientId.toString(), (data: any) => {
        this.messages.next(data);
      });
      callback();
    });
  }

  send(message: string): Observable<void> {
    let requestUrl = `${this._baseUrl}enqueuer/?code=${this._apiKey}&c=${this._clientId.toString()}`;
    console.log(`HTTP POST ${requestUrl}`);
    return this._http.post(requestUrl, message).pipe(map((result: any) => {
      for (var i = 0; i < result.length; i++) {
        result[i].isEnqueued = true;
        this.messages.next(result[i]);
      }
    }));
  }
}
