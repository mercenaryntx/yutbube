import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AppComponent } from './app.component';
import { MapToIterable } from './map-to-iterable.pipe';
import { SignalRService } from './signalr.service';
import { HttpClientModule } from '@angular/common/http';
import { MatInputModule, MatButtonModule, MatSnackBarModule } from '@angular/material';
import { MatListModule } from '@angular/material/list'

@NgModule({
  declarations: [
    AppComponent, MapToIterable
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    BrowserAnimationsModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule,
	MatListModule
  ],
  providers: [
    SignalRService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
