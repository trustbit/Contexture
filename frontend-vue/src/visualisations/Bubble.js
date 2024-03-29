import * as d3 from "d3";

function createMatrix(length) {
  const arr = new Array(length || 0);
  let i = length;

  if (arguments.length > 1) {
    const args = Array.prototype.slice.call(arguments, 1);
    while (i--) {
      arr[length - 1 - i] = createMatrix.apply(this, args);
    }
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

const BrowserText = (function () {
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");

  /**
   * Measures the rendered width of arbitrary text given the font size and font face
   * @param {string} text The text to measure
   * @param {number} fontSize The font size in pixels
   * @param {string} fontFace The font face ("Arial", "Helvetica", etc.)
   * @returns {number} The width of the text
   **/
  function getWidth(text, fontSize, fontFace = "System") {
    context.font = `${fontSize}px ${fontFace}`;
    return context.measureText(text).width;
  }

  return {
    getWidth,
  };
})();

function getFontSize(text, max_width) {
  let name_font_size = 12;
  for (let j = 12; j < 22; j++) {
    const name_width = BrowserText.getWidth(text, j);
    if (max_width < name_width) {
      break;
    }
    name_font_size = j;
  }

  return name_font_size;
}

function addTextToCircleCenter(svg, text_array, diameter, circle_index, x, y, class_name) {
  const words = [];
  for (let i = 0; i < text_array.length; i++) {
    text_array[i].split(" ").forEach((e) => {
      words.push(e);
    });
  }

  let longer_text = "";
  words.forEach((e) => {
    if (e.length > longer_text.length) {
      longer_text = e;
    }
  });

  const line_words = [];
  line_words.push("");
  words.forEach((e) => {
    if (e.length + line_words[line_words.length - 1].length < longer_text.length) {
      line_words[line_words.length - 1] = `${line_words[line_words.length - 1]} ${e}`;
    } else {
      line_words.push(e);
    }
  });

  let name_font_size = getFontSize(longer_text, diameter * 0.75);
  name_font_size = Math.min(name_font_size, (diameter * 0.75) / (line_words.length + 1));
  name_font_size = Math.max(name_font_size, 12);

  const text_element = svg
    .append("text")
    .attr("x", x)
    .attr("y", y - (line_words.length * name_font_size) / 2)
    .attr("font-size", `${name_font_size}px`);

  line_words.forEach((e) => {
    text_element
      .append("tspan")
      .attr("x", x - BrowserText.getWidth(e, name_font_size) / 2)
      .attr("dy", name_font_size)
      .attr("data-i", circle_index)
      .attr("class", class_name)
      .text(e);
  });
}

function addTextToCircleTop(svg, text_array, diameter, circle_index, x, y, class_name) {
  const words = [];
  for (let i = 0; i < text_array.length; i++) {
    text_array[i].split(" ").forEach((e) => {
      words.push(e);
    });
  }

  const longer_text = words[0];
  let longer_text_length = longer_text.length;

  const line_words = [];
  line_words.push("");
  words.forEach((e) => {
    if (e.length + line_words[line_words.length - 1].length <= longer_text_length) {
      line_words[line_words.length - 1] = `${line_words[line_words.length - 1]} ${e}`;
    } else {
      line_words.push(e);
      longer_text_length *= 1.7;
    }
  });

  const radius = diameter / 2;
  const topPadding = radius * 0.1;

  const chordaSize = 2 * Math.sqrt(radius * radius - (radius - topPadding) * (radius - topPadding));

  const name_font_size = getFontSize(longer_text, chordaSize * 0.9);
  // name_font_size = Math.min(name_font_size, diameter * 0.75 / (line_words.length + 1));

  const text_element = svg
    .append("text")
    .attr("x", x)
    .attr("y", y - radius + (line_words.length * name_font_size) / 2)
    .attr("font-size", `${name_font_size}px`);

  line_words.forEach((e) => {
    text_element
      .append("tspan")
      .attr("x", x - BrowserText.getWidth(e, name_font_size) / 2)
      .attr("dy", name_font_size)
      .attr("data-i", circle_index)
      .attr("class", class_name)
      .text(e);
  });

  // let bottom_y = y - radius + line_words.length * name_font_size / 2 + name_font_size * line_words.length;

  return name_font_size * line_words.length + topPadding;
}

function addTextToRectangle(svg, text_array, max_width, x, y, class_name) {
  let longer_text = "";
  text_array.forEach((e) => {
    if (e.length > longer_text.length) {
      longer_text = e;
    }
  });

  const name_font_size = getFontSize(longer_text, max_width);
  const long_text_width = BrowserText.getWidth(longer_text, name_font_size);

  const rect_padding = 3;
  const rect_width = long_text_width + rect_padding * 2;
  svg
    .append("rect")
    .attr("width", rect_width + 40)
    .attr("height", name_font_size * text_array.length + rect_padding * 4)
    .attr("x", x - rect_width / 2)
    .attr("y", y - rect_padding - name_font_size / 2)
    .attr("class", class_name);

  const text_element = svg
    .append("text")
    .attr("x", x - rect_width / 2 + rect_padding)
    .attr("y", y - name_font_size / 2)
    .attr("font-size", `${name_font_size}px`)
    .attr("class", class_name);

  text_array.forEach((e) => {
    text_element
      .append("tspan")
      .attr("x", x - rect_width / 2 + rect_padding)
      .attr("dy", name_font_size)
      .attr("class", class_name)
      .text(e);
  });
}

function doShowAllConnections(state, flag) {
  const shadow = state.shadow;
  const rootSvg = state.svg;
  const domain_connections = state.domain_connections;
  const domain_keys = state.domain_keys;

  rootSvg.selectAll(".chord").remove();
  if (!flag) {
    return;
  }

  const connected_circles = [];
  domain_connections.forEach((e) => {
    if (domain_keys.hasOwnProperty(e.recipient) && domain_keys.hasOwnProperty(e.initiator)) {
      connected_circles.push({
        x1: domain_keys[e.recipient].cx,
        y1: domain_keys[e.recipient].cy,
        x2: domain_keys[e.initiator].cx,
        y2: domain_keys[e.initiator].cy,
      });
    }
  });

  rootSvg
    .selectAll(".chord")
    .data(connected_circles)
    .enter()
    .append("line")
    .attr("class", "chord")
    .attr("x1", (d) => d.x1)
    .attr("y1", (d) => d.y1)
    .attr("x2", (d) => d.x2)
    .attr("y2", (d) => d.y2)
    .attr("z", 0)
    .attr("opacity", 0.2)
    .style("stroke-width", "2px")
    .style("stroke", "#0362fc");
}

function showMainPage(state) {
  state.svg.selectAll("*").remove();
  sendMessageToMoreInfo({});

  const sorted_domains = state.domains.slice(0);
  sorted_domains.sort((a, b) => {
    return b.subdomains - a.subdomains;
  });

  let box_size = sorted_domains[0].subdomains;

  let used_cells = createMatrix(box_size, box_size);
  used_cells = copyMatrix([], used_cells);

  // calculate circle coordinates
  for (let i = 0; i < sorted_domains.length; i++) {
    let found_domain_coords = false;
    const subdomains_count = Math.max(3, sorted_domains[i].subdomains);
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

          sorted_domains[i].x = k;
          sorted_domains[i].y = j;

          for (let l = j; l < j + subdomains_count; l++) {
            for (let m = k; m < k + subdomains_count; m++) {
              used_cells[l][m] = true;
            }
          }

          break;
        }
      }
      if (found_domain_coords) {
        break;
      }
    }

    if (!found_domain_coords) {
      box_size += subdomains_count;
      const tmp_matrix = createMatrix(box_size, box_size);
      used_cells = copyMatrix(used_cells, tmp_matrix);
      i--;
    }
  }

  const showDomainPageFn = showDomainPage;

  // draw circles and texts
  for (let i = 0; i < sorted_domains.length; i++) {
    const diameter = (Math.max(3, sorted_domains[i].subdomains) * state.size) / box_size;
    const x = (sorted_domains[i].x * state.size) / box_size + diameter / 2;
    const y = (sorted_domains[i].y * state.size) / box_size + diameter / 2;

    state.domain_keys[sorted_domains[i].key].cx = x;
    state.domain_keys[sorted_domains[i].key].cy = y;

    let className = "main-page";
    if (sorted_domains[i].key == "000-000") {
      className += " external-circle";
    }
    state.svg
      .append("circle")
      .attr("cx", x)
      .attr("cy", y)
      .attr("r", diameter / 2 - diameter / 10)
      .attr("opacity", 0.5)
      .attr("data-i", i)
      .attr("data-key", sorted_domains[i].key)
      .attr("class", className)
      .on("mousedown", function () {
        const key = d3.select(this).attr("data-key");
        showDomainPageFn(state, key);
      });

    addTextToCircleCenter(
      state.svg,
      [sorted_domains[i].name, `(${sorted_domains[i].subdomains})`],
      diameter,
      i,
      x,
      y,
      "main-page"
    );
  }

  const mouseoveredFn = mouseovered;
  const mouseoutedFn = mouseouted;
  // let domain_connections = this.bubbleState.domain_connections;
  // let domain_keys = this.bubbleState.domain_keys;
  // let shadow = this.bubbleState.shadow;

  state.svg
    .selectAll(".main-page")
    .on("mouseover", function () {
      const ind = d3.select(this).attr("data-i");
      mouseoveredFn(state, sorted_domains[ind].key);
    })
    .on("mouseout", () => {
      mouseoutedFn(state);
    });
}

function mouseovered(state, select_key) {
  const rootSvg = state.svg;
  const domain_connections = state.domain_connections;
  const domain_keys = state.domain_keys;

  rootSvg.selectAll(".chord").remove();

  const connected_circles = [];
  domain_connections.forEach((e) => {
    if (e.initiator == select_key || e.recipient == select_key) {
      if (domain_keys.hasOwnProperty(e.recipient) && domain_keys.hasOwnProperty(e.initiator)) {
        connected_circles.push({
          x1: domain_keys[e.recipient].cx,
          y1: domain_keys[e.recipient].cy,
          x2: domain_keys[e.initiator].cx,
          y2: domain_keys[e.initiator].cy,
        });
      }
    }
  });

  rootSvg
    .selectAll(".chord")
    .data(connected_circles)
    .enter()
    .append("line")
    .attr("class", "chord")
    .attr("x1", (d) => d.x1)
    .attr("y1", (d) => d.y1)
    .attr("x2", (d) => d.x2)
    .attr("y2", (d) => d.y2)
    .attr("opacity", 0.2)
    .style("stroke-width", "2px")
    .style("stroke", "#0362fc");
}

function mouseouted(state) {
  doShowAllConnections(state);
}

function showDomainPage(state, select_domain) {
  sendMessageToMoreInfo({
    Domain: select_domain,
  });

  state.svg.selectAll("*").remove();
  console.log(select_domain);
  const width = state.size;
  const key_arr = Object.keys(state.domain_keys);
  const out_circle_count = key_arr.length - 1;

  let alfa = 45;
  const delta_alfa = 360 / out_circle_count;
  const R = width / 4;
  const r = width / 8;
  state.out_circle_coordinates = {};

  for (let i = 0; i < key_arr.length; i++) {
    const item = state.domain_keys[key_arr[i]];
    if (key_arr[i] == select_domain) {
      state.svg
        .append("circle")
        .attr("cx", width / 2)
        .attr("cy", width / 2)
        .attr("class", "big-circle")
        .attr("r", R - 10);

      addTextToCircleCenter(
        state.svg,
        [item.name, `(${item.subdomains})`],
        R,
        i,
        width / 2,
        width / 2,
        "subdomain-circle"
      );

      const subdomain_items = item.subdomain_items;

      let betta = 0;
      const betta_delta = 360 / subdomain_items.length;
      for (let j = 0; j < subdomain_items.length; j++) {
        const subdomain = subdomain_items[j];
        const x = width / 2 + (R - 10 - r / 2) * Math.cos((betta * Math.PI) / 180);
        const y = width / 2 + (R - 10 - r / 2) * Math.sin((betta * Math.PI) / 180);

        betta += betta_delta;
        state.svg
          .append("circle")
          .attr("cx", x)
          .attr("cy", y)
          .attr("class", "subdomain-circle")
          .attr("r", r / 2 - 5)
          .attr("data-domain", select_domain)
          .attr("data-key", subdomain.key);

        addTextToCircleCenter(
          state.svg,
          [subdomain.name, `(${subdomain.boundedContexts.length})`],
          0.75 * r,
          j,
          x,
          y,
          "subdomain-circle"
        );
      }
    } else {
      const x = width / 2 + (R + r) * Math.cos((alfa * Math.PI) / 180);
      const y = width / 2 + (R + r) * Math.sin((alfa * Math.PI) / 180);

      console.log(`alfa:${alfa}`);
      console.log(`key:${item.key}`);
      alfa += delta_alfa;
      let className = "out-circle";
      if (item.key == "000-000") {
        className += " external-circle";
      }
      state.svg
        .append("circle")
        .attr("cx", x)
        .attr("cy", y)
        .attr("class", className)
        .attr("r", r - 5)
        .attr("data-key", item.key)
        .on("mousedown", function () {
          const key = d3.select(this).attr("data-key");
          showDomainPageFn(state, key);
        });

      state.out_circle_coordinates[item.key] = {
        cx: x,
        cy: y,
      };

      addTextToCircleCenter(state.svg, [item.name, `(${item.subdomains})`], R, i, x, y);
    }
  }

  const subdomain_mouseoveredFn = subdomain_mouseovered;
  const subdomain_mouseoutFn = subdomain_mouseout;
  const showSubdomainPageFn = showSubdomainPage;
  let showDomainPageFn = showDomainPage;

  state.svg
    .selectAll("circle.subdomain-circle")
    .on("mouseover", function () {
      const key = d3.select(this).attr("data-key");
      const cx = d3.select(this).attr("cx");
      const cy = d3.select(this).attr("cy");
      subdomain_mouseoveredFn(state, key, cx, cy);
    })
    .on("mouseout", () => {
      subdomain_mouseoutFn(state);
    })
    .on("mousedown", function () {
      const sel_domain = d3.select(this).attr("data-domain");
      const sel_subdomain = d3.select(this).attr("data-key");
      showSubdomainPageFn(state, sel_domain, sel_subdomain);
      // showDomainPage(key);
    });

  // Object.keys(this.domain_keys).forEach(e => {
  //     console.log(e)
  // });
}

function subdomain_mouseovered(state, select_key, x, y) {
  state.svg.selectAll(".chord").remove();

  const connected_circles = [];
  state.domain_connections.forEach((e) => {
    if (e.initiator == select_key || e.recipient == select_key) {
      if (state.out_circle_coordinates.hasOwnProperty(e.recipient)) {
        connected_circles.push({
          x1: state.out_circle_coordinates[e.recipient].cx,
          y1: state.out_circle_coordinates[e.recipient].cy,
          x2: x,
          y2: y,
        });

        console.log(`select_key:${select_key}\nrecipient:${e.recipient}`);
      }

      if (state.out_circle_coordinates.hasOwnProperty(e.initiator)) {
        connected_circles.push({
          x1: x,
          y1: y,
          x2: state.out_circle_coordinates[e.initiator].cx,
          y2: state.out_circle_coordinates[e.initiator].cy,
        });
        console.log(`select_key:${select_key}\ninitiator:${e.initiator}`);
      }
    }
  });

  state.svg
    .selectAll(".chord")
    .data(connected_circles)
    .enter()
    .append("line")
    .attr("class", "chord")
    .attr("x1", (d) => d.x1)
    .attr("y1", (d) => d.y1)
    .attr("x2", (d) => d.x2)
    .attr("y2", (d) => d.y2)
    .attr("opacity", 0.2)
    .style("stroke-width", "2px")
    .style("stroke", "#0362fc");
}

function subdomain_mouseout(state) {
  state.svg.selectAll(".chord").remove();
}

function showSubdomainPage(state, select_domain_key, select_subdomain_key) {
  sendMessageToMoreInfo({
    Domain: select_domain_key,
    SubDomain: select_subdomain_key,
  });
  // state.rootThis.setAttribute("moreinfo",select_domain_key+"/"+select_subdomain_key);

  state.svg.selectAll(".subdomain-circle").remove();
  state.svg.selectAll(".not-select-subdomain").remove();
  state.svg.selectAll(".select-subdomain").remove();
  state.svg.selectAll(".context-circle").remove();
  state.svg.selectAll(".context-circle-view").remove();
  state.svg.selectAll(".bound-context-name").remove();

  state.svg.selectAll(".debug").remove();

  state.svg.selectAll(".chord").remove();
  state.svg.selectAll(".chord-view").remove();

  console.log(`${select_domain_key} -> ${select_subdomain_key}`);

  const selec_doamin = state.domain_keys[select_domain_key];
  const subdomain_items = selec_doamin.subdomain_items;
  const width = state.size;

  const text_height = addTextToCircleTop(
    state.svg,
    [selec_doamin.name, `(${subdomain_items.length})`],
    width / 2,
    0,
    width / 2,
    width / 2,
    ""
  );

  const subdomain_diameter = (3 / 4) * (width / 2 - text_height);
  const subdomain_y = width / 2;

  state.svg
    .append("circle")
    .attr("cx", width / 2)
    .attr("cy", subdomain_y)
    .attr("class", "subdomain-circle")
    .attr("r", subdomain_diameter / 2 - 5)
    .attr("data-domain", select_domain_key)
    .attr("data-key", select_subdomain_key);

  let alfa = 0;
  const delta_alfa = 180 / Math.max(1, subdomain_items.length - 2);
  const R = subdomain_diameter / 2;
  const r = subdomain_diameter / 8;

  subdomain_items.forEach((e) => {
    if (e.key == select_subdomain_key) {
      const subdomain_text_height = addTextToCircleTop(
        state.svg,
        [e.name, `(${e.boundedContexts.length})`],
        subdomain_diameter,
        0,
        width / 2,
        subdomain_y,
        "select-subdomain"
      );

      const context_owner_circle = {
        diameter: subdomain_diameter - subdomain_text_height,
        x: width / 2,
      };
      context_owner_circle.y = subdomain_y + subdomain_text_height / 2;

      let context_alfa = 0;
      const context_delta_alfa = 360 / Math.max(1, e.boundedContexts.length);

      const context_owner_circle_radius = context_owner_circle.diameter / 2;
      let context_circle_radius = context_owner_circle_radius / 2;

      const bx0 =
        context_owner_circle.x +
        (context_owner_circle_radius - context_circle_radius - 5) * Math.cos((context_alfa * Math.PI) / 180);
      const by0 =
        context_owner_circle.y +
        (context_owner_circle_radius - context_circle_radius - 5) * Math.sin((context_alfa * Math.PI) / 180);

      const bx1 =
        context_owner_circle.x +
        (context_owner_circle_radius - context_circle_radius - 5) *
          Math.cos(((context_alfa + context_delta_alfa) * Math.PI) / 180);
      const by1 =
        context_owner_circle.y +
        (context_owner_circle_radius - context_circle_radius - 5) *
          Math.sin(((context_alfa + context_delta_alfa) * Math.PI) / 180);

      const bd = Math.sqrt((bx0 - bx1) ** 2 + (by0 - by1) ** 2);
      context_circle_radius = Math.min(context_circle_radius, 0.6 * bd);

      e.boundedContexts.forEach((ce) => {
        let bx =
          context_owner_circle.x +
          (context_owner_circle_radius - context_circle_radius - 5) * Math.cos((context_alfa * Math.PI) / 180);
        let by =
          context_owner_circle.y +
          (context_owner_circle_radius - context_circle_radius - 5) * Math.sin((context_alfa * Math.PI) / 180);

        if (e.boundedContexts.length == 1) {
          bx = context_owner_circle.x;
          by = context_owner_circle.y;
          context_circle_radius = context_owner_circle_radius * 0.9;
        }

        state.svg
          .append("circle")
          .attr("cx", bx)
          .attr("cy", by)
          .attr("r", 0.9 * context_circle_radius)
          .attr("owner_x", context_owner_circle.x)
          .attr("owner_y", context_owner_circle.y)
          .attr("owner_r", context_owner_circle_radius)
          .attr("data-domain", select_domain_key)
          .attr("data-subdomain", select_subdomain_key)
          .attr("data-key", ce.key)
          .attr("class", "context-circle");

        addTextToCircleCenter(state.svg, [ce.name], 0.9 * context_circle_radius, 0, bx, by, "context-circle");

        context_alfa += context_delta_alfa;
      });

      state.svg
        .selectAll("circle.context-circle")
        .on("mouseover", function () {
          const key = d3.select(this).attr("data-key");
          const cx = d3.select(this).attr("cx");
          const cy = d3.select(this).attr("cy");
          boundcontext_mouseovered(state, key, cx, cy);
        })
        .on("mouseout", () => {
          boundcontext_mouseout(state);
        })
        .on("mousedown", function () {
          const select_context_key = d3.select(this).attr("data-key");
          const select_domain_key = d3.select(this).attr("data-domain");
          const select_subdomain_key = d3.select(this).attr("data-subdomain");
          const cx = d3.select(this).attr("cx");
          const cy = d3.select(this).attr("cy");
          const owner_x = d3.select(this).attr("owner_x");
          const owner_y = d3.select(this).attr("owner_y");
          const owner_r = d3.select(this).attr("owner_r");
          showBoundedContextConnections(
            state,
            select_domain_key,
            select_subdomain_key,
            select_context_key,
            parseFloat(cx),
            parseFloat(cy),
            parseFloat(owner_x),
            parseFloat(owner_y),
            parseFloat(owner_r)
          );
          // let sel_domain = d3.select(this).attr('data-domain')
          // let sel_subdomain = d3.select(this).attr('data-key')
          // showSubdomainPage(sel_domain, sel_subdomain);
          // showDomainPage(key);
        });
    } else {
      const x = width / 2 + (R + r - 5) * Math.cos((alfa * Math.PI) / 180);
      const y = subdomain_y + (R + r - 5) * Math.sin((alfa * Math.PI) / 180);

      state.svg
        .append("circle")
        .attr("cx", x)
        .attr("cy", y)
        .attr("class", "not-select-circle")
        .attr("r", r - 15)
        .attr("data-domain", select_domain_key)
        .attr("data-key", e.key)
        .on("mousedown", function () {
          const sel_domain = d3.select(this).attr("data-domain");
          const sel_subdomain = d3.select(this).attr("data-key");
          showSubdomainPage(state, sel_domain, sel_subdomain);
          // showDomainPage(key);
        });

      addTextToCircleCenter(state.svg, [e.name], (r - 15) * 2, 0, x, y, "not-select-subdomain");
      alfa += delta_alfa;
    }
  });
  //
}

function boundcontext_mouseovered(state, select_key, x, y) {
  state.svg.selectAll(".chord").remove();

  const connected_circles = [];
  state.domain_connections.forEach((e) => {
    if (e.initiator == select_key || e.recipient == select_key) {
      if (state.out_circle_coordinates.hasOwnProperty(e.recipient)) {
        connected_circles.push({
          x1: state.out_circle_coordinates[e.recipient].cx,
          y1: state.out_circle_coordinates[e.recipient].cy,
          x2: x,
          y2: y,
        });

        console.log(`select_key:${select_key}\nrecipient:${e.recipient}`);
      }

      if (state.out_circle_coordinates.hasOwnProperty(e.initiator)) {
        connected_circles.push({
          x1: x,
          y1: y,
          x2: state.out_circle_coordinates[e.initiator].cx,
          y2: state.out_circle_coordinates[e.initiator].cy,
        });
        console.log(`select_key:${select_key}\ninitiator:${e.initiator}`);
      }
    }
  });

  state.svg
    .selectAll(".chord")
    .data(connected_circles)
    .enter()
    .append("line")
    .attr("class", "chord")
    .attr("x1", (d) => d.x1)
    .attr("y1", (d) => d.y1)
    .attr("x2", (d) => d.x2)
    .attr("y2", (d) => d.y2)
    .attr("opacity", 0.2)
    .style("stroke-width", "2px")
    .style("stroke", "#0362fc");
}

function boundcontext_mouseout(state) {
  state.svg.selectAll(".chord").remove();
}

function sendMessageToMoreInfo(message) {
  document.dispatchEvent(new CustomEvent("bubbleViewOnMoreInfoChanged", { detail: JSON.stringify(message) }));
}

function showBoundedContextConnections(
  state,
  select_domain_key,
  select_subdomain_key,
  select_context_key,
  x,
  y,
  owner_x,
  owner_y,
  owner_r
) {
  sendMessageToMoreInfo({
    Domain: select_context_key,
    SubDomain: select_subdomain_key,
    BoundedContext: select_context_key,
  });

  state.svg.selectAll(".chord").remove();
  state.svg.selectAll(".bound-context-name").remove();
  state.svg.selectAll(".context-circle").remove();
  state.svg.selectAll(".context-circle-view").remove();
  state.svg.selectAll(".chord-view").remove();

  const selec_doamin = state.domain_keys[select_domain_key];
  const subdomain_items = selec_doamin.subdomain_items;

  subdomain_items.forEach((e) => {
    if (e.key == select_subdomain_key) {
      const contextCount = Math.max(1, e.boundedContexts.length - 1);
      let context_alfa = -20;
      if (contextCount == 1) {
        context_alfa = 90;
      }

      const context_delta_alfa = 220 / Math.max(1, contextCount - 1);

      const boundedContexts = e.boundedContexts;
      let selContext;
      for (let i = 0; i < boundedContexts.length; i++) {
        if (boundedContexts[i].key == select_context_key) {
          selContext = boundedContexts[i];
          boundedContexts.splice(i, 1);
          boundedContexts.push(selContext);
          break;
        }
      }

      boundedContexts.forEach((ce) => {
        if (ce.key == select_context_key) {
          const select_context_cx = owner_x;
          const select_context_cy = owner_y - owner_r / 2;

          state.svg
            .append("circle")
            .attr("cx", select_context_cx)
            .attr("cy", select_context_cy)
            .attr("class", "context-circle")
            .attr("r", owner_r / 2 - 10);

          addTextToCircleCenter(
            state.svg,
            [ce.name],
            owner_r - 5,
            0,
            select_context_cx,
            select_context_cy,
            "context-circle"
          );

          const connected_circles = [];
          const texts = {};
          state.domain_connections.forEach((se) => {
            if (se.initiator == select_context_key || se.recipient == select_context_key) {
              let distance = 0;
              let xc, yc;
              if (state.out_circle_coordinates.hasOwnProperty(se.recipient)) {
                const it = {
                  x1: state.out_circle_coordinates[se.recipient].cx,
                  y1: state.out_circle_coordinates[se.recipient].cy,
                  x2: select_context_cx,
                  y2: select_context_cy,
                  name: se.name,
                };
                connected_circles.push(it);

                let desc = "(recipient)";
                if (se.name !== undefined) {
                  desc = se.name + desc;
                }

                distance = Math.sqrt((it.x1 - it.x2) ** 2 + (it.y1 - it.y2) ** 2);
                xc = (it.x1 + it.x2) / 2;
                yc = (it.y1 + it.y2) / 2;

                if (!(se.recipient in texts)) {
                  texts[se.recipient] = {
                    distance,
                    xc,
                    yc,
                    text: [],
                  };
                }

                texts[se.recipient].text.push(desc);

                console.log(`select_key:${select_context_key}\nrecipient:${se.recipient}`);
              }

              if (state.out_circle_coordinates.hasOwnProperty(se.initiator)) {
                const it = {
                  x1: select_context_cx,
                  y1: select_context_cy,
                  x2: state.out_circle_coordinates[se.initiator].cx,
                  y2: state.out_circle_coordinates[se.initiator].cy,
                  name: se.name,
                };
                connected_circles.push(it);
                distance = Math.sqrt((it.x1 - it.x2) ** 2 + (it.y1 - it.y2) ** 2);
                xc = (it.x1 + it.x2) / 2;
                yc = (it.y1 + it.y2) / 2;

                let desc = "(initiator)";
                if (se.name !== undefined) {
                  desc = se.name + desc;
                }

                if (!(se.initiator in texts)) {
                  texts[se.initiator] = {
                    distance,
                    xc,
                    yc,
                    text: [],
                  };
                }

                texts[se.initiator].text.push(desc);
                console.log(`select_key:${select_context_key}\ninitiator:${se.initiator}`);
              }
            }
          });

          state.svg
            .selectAll(".chord-view")
            .data(connected_circles)
            .enter()
            .append("line")
            .attr("class", "chord-view")
            .attr("x1", (d) => d.x1)
            .attr("y1", (d) => d.y1)
            .attr("x2", (d) => d.x2)
            .attr("y2", (d) => d.y2)
            .attr("opacity", 0.2)
            .style("stroke-width", "2px")
            .style("stroke", "#0362fc");

          const key_list = Object.keys(texts);
          key_list.forEach((k) => {
            addTextToRectangle(
              state.svg,
              texts[k].text,
              texts[k].distance / 2,
              texts[k].xc,
              texts[k].yc,
              "bound-context-name"
            );
          });
        } else {
          const bx = owner_x + (owner_r - owner_r / 4 - 5) * Math.cos((context_alfa * Math.PI) / 180);
          const by = owner_y + (owner_r - owner_r / 4 - 5) * Math.sin((context_alfa * Math.PI) / 180);

          state.svg
            .append("circle")
            .attr("cx", bx)
            .attr("cy", by)
            .attr("r", owner_r / 4)
            .attr("class", "context-circle-view")
            .attr("owner_x", owner_x)
            .attr("owner_y", owner_y)
            .attr("owner_r", owner_r)
            .attr("data-domain", select_domain_key)
            .attr("data-subdomain", select_subdomain_key)
            .attr("data-key", ce.key)
            .on("mousedown", () => {
              showBoundedContextConnections(
                state,
                select_domain_key,
                select_subdomain_key,
                ce.key,
                x,
                y,
                owner_x,
                owner_y,
                owner_r
              );
            });

          addTextToCircleCenter(state.svg, [ce.name], owner_r / 2, 0, bx, by, "context-circle-view");

          context_alfa += context_delta_alfa;
        }
      });

      state.svg
        .selectAll("circle.context-circle-view")
        .on("mouseover", function () {
          const key = d3.select(this).attr("data-key");
          const cx = d3.select(this).attr("cx");
          const cy = d3.select(this).attr("cy");
          boundcontext_mouseovered(state, key, cx, cy);
        })
        .on("mouseout", () => {
          boundcontext_mouseout(state);
        });
    }
  });
}

function calculateSizeFromHint(sizeHint) {
  const width = sizeHint.width;
  const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight);

  return Math.max(Math.min(width, height), 1000);
}

function guessWidthAndHeightFromElement(element) {
  const parentStyle = window.getComputedStyle(element);

  const width = element.clientWidth - parseFloat(parentStyle.paddingLeft) - parseFloat(parentStyle.paddingRight);
  const maxHeight =
    window.visualViewport.height -
    window.visualViewport.offsetTop -
    element.clientHeight -
    20 -
    parseFloat(parentStyle.paddingTop) -
    parseFloat(parentStyle.paddingBottom);

  return {
    width,
    maxHeight,
  };
}

function initElements(element, size) {
  // var tag = element.createElement("input");
  // tag.setAttribute("type", "checkbox");
  // tag.setAttribute("id", "show_all");
  // tag.setAttribute("onclick", "showAllConnections()");

  const width = size;
  const height = size;
  const svg = d3.select(element).append("svg").attr("width", width).attr("height", height).append("g");

  return svg;
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

const template = document.createElement("template");
template.innerHTML = `
<style>

    .node {
        font: 300 11px "Figtree", Helvetica, Arial, sans-serif;
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

    .chord {
      stroke: #0339E1
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
        fill: #CDDAFF;
    }

    circle.out-circle {
        fill: #CDDAFF
    }

    circle.external-circle{
        fill: white !important;
        stroke-dasharray: 4px;
        stroke-width: 2px;
        stroke: #d2d2d2;
    }

    circle.main-page{
        fill: #CDDAFF
    }

    circle.main-page:hover{
        fill: #e8f1fd;
    }


    circle.subdomain-circle {
        fill: #EADAFF;
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
        fill: #FFECBC;
    }

    tspan.context-circle{
        fill: black;
    }

    circle.context-circle-view{
        fill: #FFECBC;
    }

    tspan.context-circle-view{
        fill: black;
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
}

</style>
`;

export class Bubble extends HTMLElement {
  bubbleState = new BubbleState();

  constructor() {
    super();

    this.bubbleState.rootThis = this;
    this.bubbleState.shadow = this.attachShadow({ mode: "open" });
    this.bubbleState.shadow.appendChild(template.content.cloneNode(true));
  }

  async connectedCallback() {
    this.bubbleState.size = calculateSizeFromHint(guessWidthAndHeightFromElement(this.parentElement));
    this.bubbleState.svg = initElements(this.bubbleState.shadow, this.bubbleState.size);

    await this.buildData();

    const state = this.bubbleState;
    showMainPage(state);
  }

  disconnectedCallback() {
    // this.resizeObserver.disconnect();
  }

  showAllConnections(flag) {
    doShowAllConnections(this.bubbleState, flag);
  }

  showMain() {
    showMainPage(this.bubbleState);
  }

  async buildData() {
    const baseUrl = import.meta.env.VITE_CONTEXTURE_API_BASE_URL;
    const baseApi = `${baseUrl}/api`;

    const show_all_connections = false;
    const domains = [];
    const domain_keys = {};
    const bounded_context_subdomains = {};
    const bounded_context_domains = {};
    const subdomain_domains = {};

    const domain_connections = [];
    const out_circle_coordinates = {};

    const responseDomain = await fetch(`${baseApi}/domains`);
    const responseCollaborations = await fetch(`${baseApi}/collaborations`);
    const domain_data = await responseDomain.json();
    const collaboration_data = await responseCollaborations.json();

    console.log(domain_data.length);
    console.log(collaboration_data.length);

    // domain
    domain_data.forEach((e) => {
      if (!e.hasOwnProperty("parentDomainId")) {
        domain_keys[e.id] = {
          name: e.name,
          key: e.id,
          subdomain_items: [],
          subdomains: 0,
        };
      }
    });

    domain_keys["000-000"] = {
      name: "External Systems",
      key: "000-000",
      subdomain_items: [],
      subdomains: 0,
    };

    // subdomain
    domain_data.forEach((e) => {
      if (e.hasOwnProperty("parentDomainId")) {
        const domainKey = e.parentDomainId;
        const domain = domain_keys[domainKey];

        if (!domain) {
          return;
        }

        const subdomain = {
          name: e.name,
          key: e.id,
          boundedContexts: [],
        };

        e.boundedContexts.forEach((be) => {
          subdomain.boundedContexts.push({
            name: be.name,
            key: be.id,
          });

          bounded_context_domains[be.id] = e.parentDomainId;
          bounded_context_subdomains[be.id] = e.id;
        });

        domain.subdomain_items.push(subdomain);
        domain.subdomains = domain.subdomain_items.length;
        domain_keys[domainKey] = domain;

        subdomain_domains[e.id] = e.parentDomainId;
      }
    });

    const key_arr = Object.keys(domain_keys);
    key_arr.forEach((e) => {
      domains.push(domain_keys[e]);
    });

    // connections
    collaboration_data.forEach((e) => {
      const initiatorId = [];
      const recipient = [];

      if (e.initiator.hasOwnProperty("domain")) {
        initiatorId.push({
          id: e.initiator.domain,
          type: "domain",
          parent: e.initiator.domain,
          route: "initiator",
          name: e.description,
        });
      } else if (e.initiator.hasOwnProperty("boundedContext")) {
        const id = e.initiator.boundedContext;
        initiatorId.push({
          id,
          type: "boundedContext",
          name: e.description,
          parent: id,
        });
        initiatorId.push({
          id: bounded_context_subdomains[id],
          type: "context subdomain",
          name: e.description,
          parent: id,
        });
        initiatorId.push({
          id: bounded_context_domains[id],
          type: "context domain",
          name: e.description,
          parent: id,
        });
      } else {
        // add externalSystem
        const externalKeys = Object.keys(e.initiator);
        if (externalKeys.length > 0) {
          initiatorId.push({
            id: "000-000",
            type: "external system",
            name: e.initiator[externalKeys[0]],
            parent: "",
          });
        }
      }

      if (e.recipient.hasOwnProperty("domain")) {
        recipient.push({
          id: e.recipient.domain,
          type: "domain",
          parent: e.recipient.domain,
        });
      } else if (e.recipient.hasOwnProperty("boundedContext")) {
        const id = e.recipient.boundedContext;
        recipient.push({
          id,
          type: "boundedContext",
          parent: id,
        });
        recipient.push({
          id: bounded_context_subdomains[id],
          type: "context subdomain",
          parent: id,
        });
        recipient.push({
          id: bounded_context_domains[id],
          type: "context domain",
          parent: id,
        });
      } else {
        // add externalSystem
        const externalKeys = Object.keys(e.recipient);
        if (externalKeys.length > 0) {
          recipient.push({
            id: "000-000",
            type: "external system",
            name: e.recipient[externalKeys[0]],
            parent: "",
          });
        }
      }

      initiatorId.forEach((i_conn) => {
        recipient.forEach((r_conn) => {
          domain_connections.push({
            initiator: i_conn.id,
            recipient: r_conn.id,
            initiatorInfo: i_conn,
            recipientInfo: r_conn,
            name: i_conn.name,
          });
        });
      });
    });

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
