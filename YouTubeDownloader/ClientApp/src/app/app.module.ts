import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AppComponent } from './app.component';
import { SignalRService } from './signalr.service';
import { HttpClientModule } from '@angular/common/http';
import { MatInputModule, MatButtonModule, MatSnackBarModule } from '@angular/material';
import { MatListModule } from '@angular/material/list';
import { RouterModule } from '@angular/router';

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    BrowserAnimationsModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule,
    MatListModule,
    RouterModule.forRoot(
      [
        { path: "", component: AppComponent }
      ]
    )
  ],
  providers: [
    SignalRService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
