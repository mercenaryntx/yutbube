var list = /list=(PL[\w-]+)/;
var url = window.location.href;
var result = {};
var m;
if ((m = list.exec(url)) && window.confirm('Do you want to download the whole playlist?')) {
    m[1];
} else {
    var video = /www\.youtube\.com\/watch\?.*(?:v=(\w*))/;
    if ((m = video.exec(url)) == null) {
        //window.alert("Something went wrong, couldn't parse video ID.");
    } else {
        m[1];
    }
}