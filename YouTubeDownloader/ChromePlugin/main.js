//var baseUrl = 'http://localhost:4200/';
var baseUrl = 'https://yutbube.blob.core.windows.net/client/index.html';
var list = /list=(PL[\w-]+)/;
var url = window.location.href;
var m; 
if ((m = list.exec(url)) && window.confirm('Do you want to download the whole playlist?')) {
    window.open(baseUrl + '?v=' + m[1], 'yutbube');
} else {
    var video = /www\.youtube\.com\/watch\?.*(?:v=(\w*))/;
    if ((m = video.exec(url)) == null) {
        window.alert("Something went wrong, couldn't parse video ID.");
    } else {
        window.open(baseUrl + '?v=' + m[1], 'yutbube');
    }
}