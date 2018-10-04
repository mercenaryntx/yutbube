var url = window.location.href;
var i = url.indexOf('#');
if (i > 0) url = url.substring(0, i);

window.location.href = url + '#' + id;