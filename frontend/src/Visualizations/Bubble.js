import * as d3 from 'd3';

// import {fetchData} from "./DataAccess";

function createMatrix(length) {
    var arr = new Array(length || 0),
        i = length;

    if (arguments.length > 1) {
        var args = Array.prototype.slice.call(arguments, 1);
        while (i--) arr[length - 1 - i] = createMatrix.apply(this, args);
    }

    return arr;
}

function copyMatrix(old_matrix, new_matrix) {
    for (let i = 0; i < new_matrix.length; i++) {
        for (let j = 0; j < new_matrix.length; j++) {
            new_matrix[i][j] = false;
        }
    }

    for (let i = 0; i < old_matrix.length; i++) {
        for (let j = 0; j < old_matrix.length; j++) {
            new_matrix[i][j] = old_matrix[i][j];
        }
    }

    return new_matrix;
}

var BrowserText = (function () {
    var canvas = document.createElement('canvas'),
        context = canvas.getContext('2d');

    /**
     * Measures the rendered width of arbitrary text given the font size and font face
     * @param {string} text The text to measure
     * @param {number} fontSize The font size in pixels
     * @param {string} fontFace The font face ("Arial", "Helvetica", etc.)
     * @returns {number} The width of the text
     **/
    function getWidth(text, fontSize, fontFace) {
        context.font = fontSize + 'px ' + fontFace;
        return context.measureText(text).width;
    }

    return {
        getWidth: getWidth
    };
})();

function getFontSize(text, max_width) {
    let name_font_size = 1;
    for (let j = 1; j < 22; j++) {
        let name_width = BrowserText.getWidth(text, j);
        if (max_width < name_width)
            break;
        name_font_size = j;
    }

    return name_font_size;
}

function addTextToCircleCenter(svg, text_array, diameter, circle_index, x, y, class_name) {
    let words = [];
    for (let i = 0; i < text_array.length; i++) {
        text_array[i].split(' ').forEach(e => {
            words.push(e)
        })
    }


    let longer_text = '';
    words.forEach(e => {
        if (e.length > longer_text.length) longer_text = e;
    });

    let line_words = [];
    line_words.push('');
    words.forEach(e => {
        if (e.length + line_words[line_words.length - 1].length < longer_text.length) {
            line_words[line_words.length - 1] = line_words[line_words.length - 1] + ' ' + e;
        } else {
            line_words.push(e);
        }
    });


    let name_font_size = getFontSize(longer_text, diameter * 0.75);
    name_font_size = Math.min(name_font_size, diameter * 0.75 / (line_words.length + 1));

    let text_element = svg.append('text')
        .attr('x', x)
        .attr('y', y - line_words.length * name_font_size / 2)
        .attr('font-size', name_font_size + 'px');

    line_words.forEach(e => {
        text_element.append('tspan')
            .attr('x', x - BrowserText.getWidth(e, name_font_size) / 2)
            .attr('dy', name_font_size)
            .attr("data-i", circle_index)
            .attr("class", class_name)
            .text(e)
    });
}

function addTextToCircleTop(svg, text_array, diameter, circle_index, x, y, class_name) {
    let words = [];
    for (let i = 0; i < text_array.length; i++) {
        text_array[i].split(' ').forEach(e => {
            words.push(e)
        })
    }


    let longer_text = words[0];
    let longer_text_length = longer_text.length;

    let line_words = [];
    line_words.push('');
    words.forEach(e => {
        if (e.length + line_words[line_words.length - 1].length <= longer_text_length) {
            line_words[line_words.length - 1] = line_words[line_words.length - 1] + ' ' + e;
        } else {
            line_words.push(e);
            longer_text_length *= 1.7;
        }
    });

    let radius = diameter / 2;
    let topPadding = radius * 0.1;

    let chordaSize = 2 * Math.sqrt(radius * radius - (radius - topPadding) * (radius - topPadding));

    let name_font_size = getFontSize(longer_text, chordaSize * 0.9);
    // name_font_size = Math.min(name_font_size, diameter * 0.75 / (line_words.length + 1));

    let text_element = svg.append('text')
        .attr('x', x)
        .attr('y', y - radius + line_words.length * name_font_size / 2)
        .attr('font-size', name_font_size + 'px');

    line_words.forEach(e => {
        text_element.append('tspan')
            .attr('x', x - BrowserText.getWidth(e, name_font_size) / 2)
            .attr('dy', name_font_size)
            .attr("data-i", circle_index)
            .attr("class", class_name)
            .text(e)
    });

    // let bottom_y = y - radius + line_words.length * name_font_size / 2 + name_font_size * line_words.length;

    return name_font_size * line_words.length + topPadding;
}

function addTextToRectangle(svg, text_array, max_width, x, y, class_name) {
    let longer_text = '';
    text_array.forEach(e => {
        if (e.length > longer_text.length) longer_text = e;
    });

    let name_font_size = getFontSize(longer_text, max_width);

    let rect_padding = 3;
    let rect_width = max_width + rect_padding * 2;
    svg.append('rect')
        .attr('width', rect_width + 40)
        .attr('height', name_font_size * text_array.length + rect_padding * 2)
        .attr('x', x - rect_width / 2)
        .attr('y', y - rect_padding - name_font_size / 2)
        .attr("class", class_name);

    let text_element = svg.append('text')
        .attr('x', x - rect_width / 2)
        .attr('y', y - name_font_size / 2)
        .attr('font-size', name_font_size + 'px')
        .attr("class", class_name);

    text_array.forEach(e => {
        text_element.append('tspan')
            .attr('x', x - max_width / 2)
            .attr('dy', name_font_size)
            .attr("class", class_name)
            .text(e)
    });


}

function showAllConnections(state) {
    let shadow = state.shadow;
    let rootSvg = state.svg;
    let domain_connections = state.domain_connections;
    let domain_keys = state.domain_keys;


    rootSvg.selectAll(".chord").remove();
    let show_all_connections = shadow.getElementById('show_all').checked;
    if (!show_all_connections) {
        return;
    }


    let connected_circles = []
    domain_connections.forEach(e => {
        if (domain_keys.hasOwnProperty(e['recipient']) && domain_keys.hasOwnProperty(e['initiator'])) {
            connected_circles.push({
                'x1': domain_keys[e['recipient']]['cx'],
                'y1': domain_keys[e['recipient']]['cy'],
                'x2': domain_keys[e['initiator']]['cx'],
                'y2': domain_keys[e['initiator']]['cy'],
            })
        }
    });


    rootSvg.selectAll(".chord")
        .data(connected_circles)
        .enter()
        .append('line')
        .attr('class', 'chord')
        .attr('x1', d => d.x1)
        .attr('y1', d => d.y1)
        .attr('x2', d => d.x2)
        .attr('y2', d => d.y2)
        .attr('z', 0)
        .attr("opacity", 0.2)
        .style('stroke-width', "1px")
        .style('stroke', "#0362fc")
    ;
}

function showMainPage(state) {
    state.svg.selectAll("*").remove();
    state.shadow.getElementById('show_all_content').style.display = 'inline';

    var sorted_domains = state.domains.slice(0);
    sorted_domains.sort(function (a, b) {
        return b.subdomains - a.subdomains;
    });

    let box_size = sorted_domains[0].subdomains;

    let used_cells = createMatrix(box_size, box_size)
    used_cells = copyMatrix([], used_cells)


    //calculate circle coordinates
    for (let i = 0; i < sorted_domains.length; i++) {
        let found_domain_coords = false;
        let subdomains_count = Math.max(3,sorted_domains[i].subdomains);
        for (let j = 0; j <= box_size - subdomains_count; j++) {
            for (let k = 0; k <= box_size - subdomains_count; k++) {
                let is_empty = true;
                for (let l = j; l < j + subdomains_count; l++) {
                    for (let m = k; m < k + subdomains_count; m++) {
                        if (used_cells[l][m]) {
                            is_empty = false;
                            break;
                        }
                    }
                    if (!is_empty) {
                        break;
                    }

                }

                if (is_empty) {
                    found_domain_coords = true;

                    sorted_domains[i]["x"] = k;
                    sorted_domains[i]["y"] = j;

                    for (let l = j; l < j + subdomains_count; l++) {
                        for (let m = k; m < k + subdomains_count; m++) {
                            used_cells[l][m] = true;
                        }
                    }

                    break
                }

            }
            if (found_domain_coords)
                break;
        }

        if (!found_domain_coords) {
            box_size += subdomains_count;
            let tmp_matrix = createMatrix(box_size, box_size);
            used_cells = copyMatrix(used_cells, tmp_matrix);
            i--;
        }
    }

    let showDomainPageFn = showDomainPage;

    // draw circles and texts
    for (let i = 0; i < sorted_domains.length; i++) {
        let diameter = Math.max(3,sorted_domains[i]['subdomains']) * state.size / box_size;
        let x = sorted_domains[i]['x'] * state.size / box_size + diameter / 2;
        let y = sorted_domains[i]['y'] * state.size / box_size + diameter / 2;

        state.domain_keys[sorted_domains[i]['key']]['cx'] = x;
        state.domain_keys[sorted_domains[i]['key']]['cy'] = y;

        let className='main-page';
        if(sorted_domains[i]['key']=='000-000')className+=' external-circle';
        state.svg.append("circle")
            .attr("cx", x)
            .attr("cy", y)
            .attr("r", diameter / 2 - diameter / 10)
            .attr("opacity", 0.5)
            .attr("data-i", i)
            .attr("data-key", sorted_domains[i]['key'])
            .attr("class", className)
            .on("mousedown", function () {
                let key = d3.select(this).attr('data-key')
                showDomainPageFn(state, key);
            });

        addTextToCircleCenter(state.svg, [sorted_domains[i]["name"], '(' + sorted_domains[i]['subdomains'] + ')'], diameter, i, x, y, 'main-page');
    }

    let mouseoveredFn = mouseovered;
    let mouseoutedFn = mouseouted;
    // let domain_connections = this.bubbleState.domain_connections;
    // let domain_keys = this.bubbleState.domain_keys;
    // let shadow = this.bubbleState.shadow;

    state.svg.selectAll(".main-page")
        .on("mouseover", function () {
            let ind = d3.select(this).attr('data-i')
            mouseoveredFn(state, sorted_domains[ind]['key']);
        })
        .on("mouseout", function () {
            mouseoutedFn(state);
        });
}

function mouseovered(state, select_key) {
    let rootSvg = state.svg;
    let domain_connections = state.domain_connections;
    let domain_keys = state.domain_keys;

    rootSvg.selectAll(".chord").remove();

    let connected_circles = []
    domain_connections.forEach(e => {
        if (e['initiator'] == select_key || e['recipient'] == select_key) {
            if (domain_keys.hasOwnProperty(e['recipient']) && domain_keys.hasOwnProperty(e['initiator'])) {
                connected_circles.push({
                    'x1': domain_keys[e['recipient']]['cx'],
                    'y1': domain_keys[e['recipient']]['cy'],
                    'x2': domain_keys[e['initiator']]['cx'],
                    'y2': domain_keys[e['initiator']]['cy'],
                })
            }

        }
    });


    rootSvg.selectAll(".chord")
        .data(connected_circles)
        .enter()
        .append('line')
        .attr('class', 'chord')
        .attr('x1', d => d.x1)
        .attr('y1', d => d.y1)
        .attr('x2', d => d.x2)
        .attr('y2', d => d.y2)
        .attr("opacity", 0.2)
        .style('stroke-width', "1px")
        .style('stroke', "#0362fc")
    ;
}

function mouseouted(state) {
    showAllConnections(state);
}


function showDomainPage(state, select_domain) {
    state.shadow.getElementById('show_all_content').style.display = 'none';

    state.svg.selectAll("*").remove();
    console.log(select_domain)
    let width = state.size;
    let key_arr = Object.keys(state.domain_keys)
    let out_circle_count = key_arr.length - 1;

    let alfa = 45;
    let delta_alfa = 360 / out_circle_count;
    let R = width / 4;
    let r = width / 8;
    state.out_circle_coordinates = {};


    for (let i = 0; i < key_arr.length; i++) {
        let item = state.domain_keys[key_arr[i]];
        if (key_arr[i] == select_domain) {

            state.svg.append("circle")
                .attr("cx", width / 2)
                .attr("cy", width / 2)
                .attr("class", 'big-circle')
                .attr("r", R - 10);

            addTextToCircleCenter(state.svg, [item['name'], '(' + item['subdomains'] + ')'], R, i, width / 2, width / 2, 'subdomain-circle');

            let subdomain_items = item["subdomain_items"];

            let betta = 0;
            let betta_delta = 360 / subdomain_items.length;
            for (let j = 0; j < subdomain_items.length; j++) {
                let subdomain = subdomain_items[j];
                let x = width / 2 + (R - 10 - r / 2) * Math.cos(betta * Math.PI / 180);
                let y = width / 2 + (R - 10 - r / 2) * Math.sin(betta * Math.PI / 180);

                betta += betta_delta;
                state.svg.append("circle")
                    .attr("cx", x)
                    .attr("cy", y)
                    .attr("class", 'subdomain-circle')
                    .attr("r", r / 2 - 5)
                    .attr("data-domain", select_domain)
                    .attr("data-key", subdomain['key']);


                addTextToCircleCenter(state.svg, [subdomain['name'], '(' + subdomain['boundedContexts'].length + ')'], 0.75 * r, j, x, y, 'subdomain-circle')

            }


        } else {
            let x = width / 2 + (R + r) * Math.cos(alfa * Math.PI / 180);
            let y = width / 2 + (R + r) * Math.sin(alfa * Math.PI / 180);

            console.log('alfa:' + alfa)
            console.log('key:' + item['key'])
            alfa += delta_alfa;
            let className='out-circle';
            if(item['key']=='000-000') className+=' external-circle';
            state.svg.append("circle")
                .attr("cx", x)
                .attr("cy", y)
                .attr("class", className)
                .attr("r", r - 5)
                .attr("data-key", item['key'])
                .on("mousedown", function () {
                    let key = d3.select(this).attr('data-key')
                    showDomainPageFn(state, key);
                });

            state.out_circle_coordinates[item['key']] = {'cx': x, 'cy': y};

            addTextToCircleCenter(state.svg, [item['name'], '(' + item['subdomains'] + ')'], R, i, x, y)
        }
    }

    let subdomain_mouseoveredFn = subdomain_mouseovered;
    let subdomain_mouseoutFn = subdomain_mouseout;
    let showSubdomainPageFn = showSubdomainPage;
    let showDomainPageFn = showDomainPage;


    state.svg.selectAll("circle.subdomain-circle")
        .on("mouseover", function () {
            let key = d3.select(this).attr('data-key');
            let cx = d3.select(this).attr('cx');
            let cy = d3.select(this).attr('cy');
            subdomain_mouseoveredFn(state, key, cx, cy);
        })
        .on("mouseout", function () {
            subdomain_mouseoutFn(state)

        })
        .on("mousedown", function () {
            let sel_domain = d3.select(this).attr('data-domain')
            let sel_subdomain = d3.select(this).attr('data-key')
            showSubdomainPageFn(state, sel_domain, sel_subdomain);
            // showDomainPage(key);
        });


    // Object.keys(this.domain_keys).forEach(e => {
    //     console.log(e)
    // });


}


function subdomain_mouseovered(state, select_key, x, y) {
    state.svg.selectAll(".chord").remove();

    let connected_circles = []
    state.domain_connections.forEach(e => {
        if (e['initiator'] == select_key || e['recipient'] == select_key) {
            if (state.out_circle_coordinates.hasOwnProperty(e['recipient'])) {
                connected_circles.push({
                    'x1': state.out_circle_coordinates[e['recipient']]['cx'],
                    'y1': state.out_circle_coordinates[e['recipient']]['cy'],
                    'x2': x,
                    'y2': y,
                });

                console.log('select_key:' + select_key + '\nrecipient:' + e['recipient']);
            }

            if (state.out_circle_coordinates.hasOwnProperty(e['initiator'])) {
                connected_circles.push({
                    'x1': x,
                    'y1': y,
                    'x2': state.out_circle_coordinates[e['initiator']]['cx'],
                    'y2': state.out_circle_coordinates[e['initiator']]['cy'],
                });
                console.log('select_key:' + select_key + '\ninitiator:' + e['initiator']);

            }

        }

    });


    state.svg.selectAll(".chord")
        .data(connected_circles)
        .enter()
        .append('line')
        .attr('class', 'chord')
        .attr('x1', d => d.x1)
        .attr('y1', d => d.y1)
        .attr('x2', d => d.x2)
        .attr('y2', d => d.y2)
        .attr("opacity", 0.2)
        .style('stroke-width', "1px")
        .style('stroke', "#0362fc")
    ;
}

function subdomain_mouseout(state) {
    state.svg.selectAll(".chord").remove();
}

function showSubdomainPage(state, select_domain_key, select_subdomain_key) {
    state.svg.selectAll(".subdomain-circle").remove();
    state.svg.selectAll(".select-subdomain").remove();
    state.svg.selectAll(".context-circle").remove();
    state.svg.selectAll(".context-circle-view").remove();
    state.svg.selectAll(".bound-context-name").remove();

    state.svg.selectAll(".debug").remove();


    state.svg.selectAll(".chord").remove();
    state.svg.selectAll(".chord-view").remove();

    console.log(select_domain_key + ' -> ' + select_subdomain_key);

    let selec_doamin = state.domain_keys[select_domain_key];
    let subdomain_items = selec_doamin["subdomain_items"];
    let width = state.size;

    let text_height = addTextToCircleTop(state.svg, [selec_doamin['name'], '(' + subdomain_items.length + ')'], width / 2, 0, width / 2, width / 2, '');

    let subdomain_diameter = 3 / 4 * (width / 2 - text_height);
    let subdomain_y = width / 2;

    state.svg.append("circle")
        .attr("cx", width / 2)
        .attr("cy", subdomain_y)
        .attr("class", 'subdomain-circle')
        .attr("r", subdomain_diameter / 2 - 5)
        .attr("data-domain", select_domain_key)
        .attr("data-key", select_subdomain_key);

    let alfa = 0;
    let delta_alfa = 180 / (Math.max(1, subdomain_items.length - 2));
    let R = subdomain_diameter / 2;
    let r = subdomain_diameter / 8;

    subdomain_items.forEach(e => {
        if (e['key'] == select_subdomain_key) {
            let subdomain_text_height = addTextToCircleTop(state.svg, [e['name'], '(' + e['boundedContexts'].length + ')'], subdomain_diameter, 0, width / 2, subdomain_y, 'select-subdomain');

            let context_owner_circle = {
                'diameter': subdomain_diameter - subdomain_text_height,
                'x': width / 2,
            }
            context_owner_circle['y'] = subdomain_y + subdomain_text_height / 2;


            let context_alfa = 0;
            let context_delta_alfa = 360 / Math.max(1, e['boundedContexts'].length);

            let context_owner_circle_radius = context_owner_circle['diameter'] / 2;
            let context_circle_radius = context_owner_circle_radius / 2;

            let bx0 = context_owner_circle['x'] + (context_owner_circle_radius - context_circle_radius - 5) * Math.cos(context_alfa * Math.PI / 180);
            let by0 = context_owner_circle['y'] + (context_owner_circle_radius - context_circle_radius - 5) * Math.sin(context_alfa * Math.PI / 180);

            let bx1 = context_owner_circle['x'] + (context_owner_circle_radius - context_circle_radius - 5) * Math.cos((context_alfa + context_delta_alfa) * Math.PI / 180);
            let by1 = context_owner_circle['y'] + (context_owner_circle_radius - context_circle_radius - 5) * Math.sin((context_alfa + context_delta_alfa) * Math.PI / 180);

            let bd = Math.sqrt(Math.pow(bx0 - bx1, 2) + Math.pow(by0 - by1, 2));
            context_circle_radius = Math.min(context_circle_radius, 0.6 * bd);

            e['boundedContexts'].forEach(ce => {
                let bx = context_owner_circle['x'] + (context_owner_circle_radius - context_circle_radius - 5) * Math.cos(context_alfa * Math.PI / 180);
                let by = context_owner_circle['y'] + (context_owner_circle_radius - context_circle_radius - 5) * Math.sin(context_alfa * Math.PI / 180);

                if (e['boundedContexts'].length == 1) {
                    bx = context_owner_circle['x'];
                    by = context_owner_circle['y']
                    context_circle_radius = context_owner_circle_radius * 0.9;
                }

                state.svg.append("circle")
                    .attr("cx", bx)
                    .attr("cy", by)
                    .attr("r", 0.9 * context_circle_radius)
                    .attr("owner_x", context_owner_circle['x'])
                    .attr("owner_y", context_owner_circle['y'])
                    .attr("owner_r", context_owner_circle_radius)
                    .attr("data-domain", select_domain_key)
                    .attr("data-subdomain", select_subdomain_key)
                    .attr("data-key", ce['key'])
                    .attr("class", 'context-circle');

                addTextToCircleCenter(state.svg, [ce['name']], 0.9 * context_circle_radius, 0, bx, by, 'context-circle');

                context_alfa += context_delta_alfa;
            });

            state.svg.selectAll("circle.context-circle")
                .on("mouseover", function () {
                    let key = d3.select(this).attr('data-key');
                    let cx = d3.select(this).attr('cx');
                    let cy = d3.select(this).attr('cy');
                    boundcontext_mouseovered(state, key, cx, cy);
                })
                .on("mouseout", function () {
                    boundcontext_mouseout(state);
                })
                .on("mousedown", function () {
                    let select_context_key = d3.select(this).attr('data-key');
                    let select_domain_key = d3.select(this).attr('data-domain');
                    let select_subdomain_key = d3.select(this).attr('data-subdomain');
                    let cx = d3.select(this).attr('cx');
                    let cy = d3.select(this).attr('cy');
                    let owner_x = d3.select(this).attr('owner_x');
                    let owner_y = d3.select(this).attr('owner_y');
                    let owner_r = d3.select(this).attr('owner_r');
                    showBoundedContextConnections(state, select_domain_key, select_subdomain_key, select_context_key, parseFloat(cx), parseFloat(cy), parseFloat(owner_x), parseFloat(owner_y), parseFloat(owner_r))
                    // let sel_domain = d3.select(this).attr('data-domain')
                    // let sel_subdomain = d3.select(this).attr('data-key')
                    // showSubdomainPage(sel_domain, sel_subdomain);
                    // showDomainPage(key);
                });

        } else {
            let x = width / 2 + (R + r - 5) * Math.cos(alfa * Math.PI / 180);
            let y = subdomain_y + (R + r - 5) * Math.sin(alfa * Math.PI / 180);

            state.svg.append("circle")
                .attr("cx", x)
                .attr("cy", y)
                .attr("class", 'not-select-circle')
                .attr("r", r - 15)
                .attr("data-domain", select_domain_key)
                .attr("data-key", e['key'])
                .on("mousedown", function () {
                    let sel_domain = d3.select(this).attr('data-domain')
                    let sel_subdomain = d3.select(this).attr('data-key')
                    showSubdomainPage(state, sel_domain, sel_subdomain);
                    // showDomainPage(key);
                });

            addTextToCircleCenter(state.svg, [e['name']], (r - 15) * 2, 0, x, y, 'not-select-subdomain')
            alfa += delta_alfa;
        }
    });
    //
}

function boundcontext_mouseovered(state, select_key, x, y) {
    state.svg.selectAll(".chord").remove();

    let connected_circles = []
    state.domain_connections.forEach(e => {
        if (e['initiator'] == select_key || e['recipient'] == select_key) {
            if (state.out_circle_coordinates.hasOwnProperty(e['recipient'])) {
                connected_circles.push({
                    'x1': state.out_circle_coordinates[e['recipient']]['cx'],
                    'y1': state.out_circle_coordinates[e['recipient']]['cy'],
                    'x2': x,
                    'y2': y,
                })

                console.log('select_key:' + select_key + '\nrecipient:' + e['recipient']);
            }

            if (state.out_circle_coordinates.hasOwnProperty(e['initiator'])) {
                connected_circles.push({
                    'x1': x,
                    'y1': y,
                    'x2': state.out_circle_coordinates[e['initiator']]['cx'],
                    'y2': state.out_circle_coordinates[e['initiator']]['cy'],
                })
                console.log('select_key:' + select_key + '\ninitiator:' + e['initiator']);

            }

        }

    });


    state.svg.selectAll(".chord")
        .data(connected_circles)
        .enter()
        .append('line')
        .attr('class', 'chord')
        .attr('x1', d => d.x1)
        .attr('y1', d => d.y1)
        .attr('x2', d => d.x2)
        .attr('y2', d => d.y2)
        .attr("opacity", 0.2)
        .style('stroke-width', "1px")
        .style('stroke', "#0362fc")
    ;
}

function boundcontext_mouseout(state) {
    state.svg.selectAll(".chord").remove();
}

function showBoundedContextConnections(state, select_domain_key, select_subdomain_key, select_context_key, x, y, owner_x, owner_y, owner_r) {
    state.svg.selectAll(".chord").remove();
    state.svg.selectAll(".bound-context-name").remove();
    state.svg.selectAll(".context-circle").remove();
    state.svg.selectAll(".context-circle-view").remove();
    state.svg.selectAll(".chord-view").remove();

    let selec_doamin = state.domain_keys[select_domain_key];
    let subdomain_items = selec_doamin["subdomain_items"];

    subdomain_items.forEach(e => {
        if (e['key'] == select_subdomain_key) {
            let contextCount = Math.max(1, e['boundedContexts'].length - 1);
            let context_alfa = -20;
            if (contextCount == 1) context_alfa = 90;

            let context_delta_alfa = 220 / Math.max(1, contextCount - 1);

            let boundedContexts = e['boundedContexts'];
            let selContext;
            for (let i = 0; i < boundedContexts.length; i++) {
                if (boundedContexts[i]['key'] == select_context_key) {
                    selContext = boundedContexts[i];
                    boundedContexts.splice(i, 1);
                    boundedContexts.push(selContext);
                    break;
                }
            }


            boundedContexts.forEach(ce => {
                if (ce['key'] == select_context_key) {
                    let select_context_cx = owner_x;
                    let select_context_cy = owner_y - owner_r / 2;

                    state.svg.append("circle")
                        .attr("cx", select_context_cx)
                        .attr("cy", select_context_cy)
                        .attr("class", 'context-circle')
                        .attr("r", owner_r / 2 - 10);

                    addTextToCircleCenter(state.svg, [ce['name']], owner_r - 5, 0, select_context_cx, select_context_cy, 'context-circle');

                    let connected_circles = [];
                    let texts = {};
                    state.domain_connections.forEach(e => {
                        if (e['initiator'] == select_context_key || e['recipient'] == select_context_key) {
                            let distance = 0;
                            let xc, yc;
                            if (state.out_circle_coordinates.hasOwnProperty(e['recipient'])) {
                                let it = {
                                    'x1': state.out_circle_coordinates[e['recipient']]['cx'],
                                    'y1': state.out_circle_coordinates[e['recipient']]['cy'],
                                    'x2': select_context_cx,
                                    'y2': select_context_cy,
                                    'name': e['name']
                                };
                                connected_circles.push(it);

                                let desc = '(recipient)';
                                if (e['name'] !== undefined)
                                    desc = e['name'] + desc;

                                distance = Math.sqrt(Math.pow(it['x1'] - it['x2'], 2) + Math.pow(it['y1'] - it['y2'], 2));
                                xc = (it['x1'] + it['x2']) / 2;
                                yc = (it['y1'] + it['y2']) / 2;

                                if (!(e['recipient'] in texts)) {
                                    texts[e['recipient']] = {
                                        'distance': distance,
                                        'xc': xc,
                                        'yc': yc,
                                        'text': []
                                    }
                                }

                                texts[e['recipient']]['text'].push(desc);

                                console.log('select_key:' + select_context_key + '\nrecipient:' + e['recipient']);
                            }

                            if (state.out_circle_coordinates.hasOwnProperty(e['initiator'])) {
                                let it = {
                                    'x1': select_context_cx,
                                    'y1': select_context_cy,
                                    'x2': state.out_circle_coordinates[e['initiator']]['cx'],
                                    'y2': state.out_circle_coordinates[e['initiator']]['cy'],
                                    'name': e['name']
                                };
                                connected_circles.push(it);
                                distance = Math.sqrt(Math.pow(it['x1'] - it['x2'], 2) + Math.pow(it['y1'] - it['y2'], 2));
                                xc = (it['x1'] + it['x2']) / 2;
                                yc = (it['y1'] + it['y2']) / 2;

                                let desc = '(initiator)';
                                if (e['name'] !== undefined)
                                    desc = e['name'] + desc;

                                if (!(e['initiator'] in texts)) {
                                    texts[e['initiator']] = {
                                        'distance': distance,
                                        'xc': xc,
                                        'yc': yc,
                                        'text': []
                                    }
                                }

                                texts[e['initiator']]['text'].push(desc);
                                console.log('select_key:' + select_context_key + '\ninitiator:' + e['initiator']);

                            }


                        }

                    });

                    state.svg.selectAll(".chord-view")
                        .data(connected_circles)
                        .enter()
                        .append('line')
                        .attr('class', 'chord-view')
                        .attr('x1', d => d.x1)
                        .attr('y1', d => d.y1)
                        .attr('x2', d => d.x2)
                        .attr('y2', d => d.y2)
                        .attr("opacity", 0.2)
                        .style('stroke-width', "1px")
                        .style('stroke', "#0362fc")
                    ;

                    let key_list = Object.keys(texts);
                    key_list.forEach(k => {
                        addTextToRectangle(state.svg, texts[k]['text'], texts[k]['distance'] / 2, texts[k]['xc'], texts[k]['yc'], 'bound-context-name')
                    });

                } else {


                    let bx = owner_x + (owner_r - owner_r / 4 - 5) * Math.cos(context_alfa * Math.PI / 180);
                    let by = owner_y + (owner_r - owner_r / 4 - 5) * Math.sin(context_alfa * Math.PI / 180);

                    state.svg.append("circle")
                        .attr("cx", bx)
                        .attr("cy", by)
                        .attr("r", owner_r / 4)
                        .attr("class", 'context-circle-view')
                        .attr("owner_x", owner_x)
                        .attr("owner_y", owner_y)
                        .attr("owner_r", owner_r)
                        .attr("data-domain", select_domain_key)
                        .attr("data-subdomain", select_subdomain_key)
                        .attr("data-key", ce['key'])
                        .on("mousedown", function () {
                            showBoundedContextConnections(state, select_domain_key, select_subdomain_key, ce['key'], x, y, owner_x, owner_y, owner_r)
                        });

                    addTextToCircleCenter(state.svg, [ce['name']], owner_r / 2, 0, bx, by, 'context-circle-view');


                    context_alfa += context_delta_alfa;
                }
            });

            state.svg.selectAll("circle.context-circle-view")
                .on("mouseover", function () {
                    let key = d3.select(this).attr('data-key');
                    let cx = d3.select(this).attr('cx');
                    let cy = d3.select(this).attr('cy');
                    boundcontext_mouseovered(state, key, cx, cy);
                })
                .on("mouseout", function () {
                    boundcontext_mouseout(state);
                });

        }
    })


}


function calculateSizeFromHint(sizeHint) {
    const width = sizeHint.width;
    const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight)

    return Math.max(Math.min(width, height), 1000)
}

function guessWidthAndHeightFromElement(element) {
    const parentStyle = window.getComputedStyle(element);
    const width =
        element.clientWidth
        - parseFloat(parentStyle.paddingLeft)
        - parseFloat(parentStyle.paddingRight);
    const maxHeight = Math.max(400,
        element.clientHeight
        - parseFloat(parentStyle.paddingTop)
        - parseFloat(parentStyle.paddingBottom))
    ;
    return {width: width, maxHeight: maxHeight};
}

function initElements(element, size) {
    // var tag = element.createElement("input");
    // tag.setAttribute("type", "checkbox");
    // tag.setAttribute("id", "show_all");
    // tag.setAttribute("onclick", "showAllConnections()");


    let width = size;
    let height = size;
    const svg = d3
        .select(element)
        .append("svg")
        .attr("width", width)
        .attr("height", height)
        .append("g");

    return svg
}

class BubbleState {
    show_all_connections = false;
    domains = [];
    domain_keys = {};
    bounded_context_subdomains = {};
    bounded_context_domains = {};
    subdomain_domains = {};

    domain_connections = [];
    out_circle_coordinates = {};
    svg = {};
    shadow = {};
    size = 0;
}


const template = document.createElement('template');
template.innerHTML = `
<style>

    .node {
        font: 300 11px "Helvetica Neue", Helvetica, Arial, sans-serif;
        fill: #bbb;
    }

    .node:hover {
        fill: #000;
    }

    .link {
        stroke: steelblue;
        stroke-opacity: 0.4;
        fill: none;
        pointer-events: none;
    }

    .node:hover,
    .node--source,
    .node--target {
        font-weight: 700;
    }

    .node--source {
        fill: #2ca02c;
    }

    .node--target {
        fill: #d62728;
    }

    .link--source,
    .link--target {
        stroke-opacity: 1;
        stroke-width: 2px;
    }

    .link--source {
        stroke: #d62728;
    }

    .link--target {
        stroke: #2ca02c;
    }

    circle, tspan {
        z-index: 2;
    }

    circle {
        cursor: pointer;
    }

    tspan {
        z-index: 3;
    }

    line {
        z-index: 1;
        opacity: 0.5;
    }

    circle.big-circle {
        fill: #e8f1fd;
    }

    circle.out-circle {
        fill: #f1f1f1;
    }
    
    circle.external-circle{
        fill: white !important;
        stroke-dasharray: 4px;
        stroke-width: 2px;
        stroke: #d2d2d2;
    }
    
    circle.main-page{
        fill: #f1f1f1;
    }
    
    circle.main-page:hover{
        fill: #e8f1fd;
    }
    

    circle.subdomain-circle {
        fill: #add0ff;
    }

    circle.not-select-circle {
        stroke-dasharray: 4px;
        stroke-width: 2px;
        fill: #e8f1fd;
        stroke: #d2d2d2;
    }

    circle.debug {
        fill: red;
    }

    circle.context-circle {
        fill: #017cff;
    }
    
    tspan.context-circle{
        fill: white;
    }
    
    circle.context-circle-view{
        fill: #017cff;
    }
    
    tspan.context-circle-view{
        fill: white;
    }
    
    tspan.bound-context-name{
        fill:white;
    }
    rect.bound-context-name{
        fill:#0058d3;
        stroke: white;
    }
    
    .btn {
    display: inline-block;
    font-weight: 400;
    line-height: 1.5;
    color: #212529;
    text-align: center;
    text-decoration: none;
    vertical-align: middle;
    cursor: pointer;
    -webkit-user-select: none;
    -moz-user-select: none;
    user-select: none;
    background-color: transparent;
    border: 1px solid transparent;
    padding: 0 0;
    font-size: 1rem;
    border-radius: 0.25rem;
    transition: color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;
}


    
    .btn-link {
    font-weight: 400;
    color: #0d6efd;
    text-decoration: underline;
}

</style>
`;

export class Bubble extends HTMLElement {
    bubbleState = new BubbleState();

    constructor() {
        super();


        this.bubbleState.shadow = this.attachShadow({mode: 'open'});
        this.bubbleState.shadow.appendChild(template.content.cloneNode(true));
        this.bubbleState.shadow.innerHTML += '<label id="show_all_content"><input id="show_all" type="checkbox">Show all connections</label><br>' +
            '<button type="button" class="btn btn-link" id="go_home">Home</button></br>';
    }


    async connectedCallback() {
        this.bubbleState.size = calculateSizeFromHint(guessWidthAndHeightFromElement(this.parentElement));
        this.bubbleState.svg = initElements(this.bubbleState.shadow, this.bubbleState.size);


        await this.buildData();

        let element = this.bubbleState.shadow.getElementById('show_all');

        let state = this.bubbleState;
        element.onclick = function () {
            showAllConnections(state);
        };

        element = this.bubbleState.shadow.getElementById('go_home');
        element.onclick = function () {
            showMainPage(state);
        };

        showMainPage(state);
    }

    disconnectedCallback() {
        // this.resizeObserver.disconnect();
    }

    async buildData() {
        console.log('start');
        const baseApi = this.getAttribute('baseApi');

        let show_all_connections = false;
        let domains = [];
        let domain_keys = {};
        let bounded_context_subdomains = {};
        let bounded_context_domains = {};
        let subdomain_domains = {};

        let domain_connections = [];
        let out_circle_coordinates = {}

        const responseDomain = await fetch(`${baseApi}domains`);
        const responseCollaborations = await fetch(`${baseApi}collaborations`);
        let domain_data = await responseDomain.json();
        let collaboration_data = await responseCollaborations.json();

        console.log(domain_data.length);
        console.log(collaboration_data.length);

        //domain
        domain_data.forEach(e => {
            if (!e.hasOwnProperty('parentDomainId')) {
                domain_keys[e['id']] = {
                    'name': e['name'],
                    'key': e['id'],
                    'subdomain_items': [],
                    'subdomains': 0,
                }
            }
        });

        domain_keys['000-000'] = {
            'name': 'External Systems',
            'key': '000-000',
            'subdomain_items': [],
            'subdomains': 0,
        };


        //subdomain
        domain_data.forEach(e => {
            if (e.hasOwnProperty('parentDomainId')) {
                let domainKey = e['parentDomainId'];
                let domain = domain_keys[domainKey];

                let subdomain = {
                    'name': e['name'],
                    'key': e['id'],
                    'boundedContexts': []
                };

                e['boundedContexts'].forEach(be => {
                    subdomain['boundedContexts'].push({
                        'name': be['name'],
                        'key': be['id']
                    });

                    bounded_context_domains[be['id']] = e['parentDomainId'];
                    bounded_context_subdomains[be['id']] = e['id'];
                });

                domain['subdomain_items'].push(subdomain);
                domain['subdomains'] = domain['subdomain_items'].length;
                domain_keys[domainKey] = domain;

                subdomain_domains[e['id']] = e['parentDomainId'];
            }
        });

        let key_arr = Object.keys(domain_keys);
        key_arr.forEach(e => {
            domains.push(domain_keys[e])
        });


        //connections
        collaboration_data.forEach(e => {
            let initiatorId = [], recipient = [];

            if (e.initiator.hasOwnProperty('domain')) initiatorId.push({
                'id': e.initiator.domain,
                'type': 'domain',
                'parent': e.initiator.domain,
                'route': 'initiator',
                'name': e.description
            });
            else if (e.initiator.hasOwnProperty('boundedContext')) {
                let id = e.initiator.boundedContext;
                initiatorId.push({
                    'id': id,
                    'type': 'boundedContext',
                    'name': e.description,
                    'parent': id
                });
                initiatorId.push({
                    'id': bounded_context_subdomains[id],
                    'type': 'context subdomain',
                    'name': e.description,
                    'parent': id
                });
                initiatorId.push({
                    'id': bounded_context_domains[id],
                    'type': 'context domain',
                    'name': e.description,
                    'parent': id
                });
            } else {
                //add externalSystem
                let externalKeys = Object.keys(e.initiator);
                if (externalKeys.length > 0) {
                    initiatorId.push({
                        'id': '000-000',
                        'type': 'external system',
                        'name': e.initiator[externalKeys[0]],
                        'parent': ''
                    });
                }

            }

            if (e.recipient.hasOwnProperty('domain')) recipient.push({
                'id': e.recipient.domain,
                'type': 'domain',
                'parent': e.recipient.domain
            });
            else if (e.recipient.hasOwnProperty('boundedContext')) {
                let id = e.recipient.boundedContext;
                recipient.push({
                    'id': id,
                    'type': 'boundedContext',
                    'parent': id
                });
                recipient.push({
                    'id': bounded_context_subdomains[id],
                    'type': 'context subdomain',
                    'parent': id
                });
                recipient.push({
                    'id': bounded_context_domains[id],
                    'type': 'context domain',
                    'parent': id
                });
            } else {
                //add externalSystem
                let externalKeys = Object.keys(e.recipient);
                if (externalKeys.length > 0) {
                    recipient.push({
                        'id': '000-000',
                        'type': 'external system',
                        'name': e.recipient[externalKeys[0]],
                        'parent': ''
                    });
                }
            }

            initiatorId.forEach(i_conn => {
                recipient.forEach(r_conn => {
                    domain_connections.push({
                        "initiator": i_conn['id'],
                        "recipient": r_conn['id'],
                        'initiatorInfo': i_conn,
                        'recipientInfo': r_conn,
                        'name': i_conn['name']
                    })
                })
            })
        });

        console.log('end')

        this.bubbleState.show_all_connections = show_all_connections;
        this.bubbleState.domains = domains;
        this.bubbleState.domain_keys = domain_keys;
        this.bubbleState.bounded_context_subdomains = bounded_context_subdomains;
        this.bubbleState.bounded_context_domains = bounded_context_domains;
        this.bubbleState.subdomain_domains = subdomain_domains;
        this.bubbleState.domain_connections = domain_connections;
        this.bubbleState.out_circle_coordinates = out_circle_coordinates;
    }


}

