var Contexture = (function (exports) {
  'use strict';

  function descending(a, b) {
    return a == null || b == null ? NaN
      : b < a ? -1
      : b > a ? 1
      : b >= a ? 0
      : NaN;
  }

  class InternMap extends Map {
    constructor(entries, key = keyof) {
      super();
      Object.defineProperties(this, {_intern: {value: new Map()}, _key: {value: key}});
      if (entries != null) for (const [key, value] of entries) this.set(key, value);
    }
    get(key) {
      return super.get(intern_get(this, key));
    }
    has(key) {
      return super.has(intern_get(this, key));
    }
    set(key, value) {
      return super.set(intern_set(this, key), value);
    }
    delete(key) {
      return super.delete(intern_delete(this, key));
    }
  }

  function intern_get({_intern, _key}, value) {
    const key = _key(value);
    return _intern.has(key) ? _intern.get(key) : value;
  }

  function intern_set({_intern, _key}, value) {
    const key = _key(value);
    if (_intern.has(key)) return _intern.get(key);
    _intern.set(key, value);
    return value;
  }

  function intern_delete({_intern, _key}, value) {
    const key = _key(value);
    if (_intern.has(key)) {
      value = _intern.get(value);
      _intern.delete(key);
    }
    return value;
  }

  function keyof(value) {
    return value !== null && typeof value === "object" ? value.valueOf() : value;
  }

  function sum(values, valueof) {
    let sum = 0;
    if (valueof === undefined) {
      for (let value of values) {
        if (value = +value) {
          sum += value;
        }
      }
    } else {
      let index = -1;
      for (let value of values) {
        if (value = +valueof(value, ++index, values)) {
          sum += value;
        }
      }
    }
    return sum;
  }

  var noop = {value: () => {}};

  function dispatch() {
    for (var i = 0, n = arguments.length, _ = {}, t; i < n; ++i) {
      if (!(t = arguments[i] + "") || (t in _) || /[\s.]/.test(t)) throw new Error("illegal type: " + t);
      _[t] = [];
    }
    return new Dispatch(_);
  }

  function Dispatch(_) {
    this._ = _;
  }

  function parseTypenames$1(typenames, types) {
    return typenames.trim().split(/^|\s+/).map(function(t) {
      var name = "", i = t.indexOf(".");
      if (i >= 0) name = t.slice(i + 1), t = t.slice(0, i);
      if (t && !types.hasOwnProperty(t)) throw new Error("unknown type: " + t);
      return {type: t, name: name};
    });
  }

  Dispatch.prototype = dispatch.prototype = {
    constructor: Dispatch,
    on: function(typename, callback) {
      var _ = this._,
          T = parseTypenames$1(typename + "", _),
          t,
          i = -1,
          n = T.length;

      // If no callback was specified, return the callback of the given type and name.
      if (arguments.length < 2) {
        while (++i < n) if ((t = (typename = T[i]).type) && (t = get$1(_[t], typename.name))) return t;
        return;
      }

      // If a type was specified, set the callback for the given type and name.
      // Otherwise, if a null callback was specified, remove callbacks of the given name.
      if (callback != null && typeof callback !== "function") throw new Error("invalid callback: " + callback);
      while (++i < n) {
        if (t = (typename = T[i]).type) _[t] = set$1(_[t], typename.name, callback);
        else if (callback == null) for (t in _) _[t] = set$1(_[t], typename.name, null);
      }

      return this;
    },
    copy: function() {
      var copy = {}, _ = this._;
      for (var t in _) copy[t] = _[t].slice();
      return new Dispatch(copy);
    },
    call: function(type, that) {
      if ((n = arguments.length - 2) > 0) for (var args = new Array(n), i = 0, n, t; i < n; ++i) args[i] = arguments[i + 2];
      if (!this._.hasOwnProperty(type)) throw new Error("unknown type: " + type);
      for (t = this._[type], i = 0, n = t.length; i < n; ++i) t[i].value.apply(that, args);
    },
    apply: function(type, that, args) {
      if (!this._.hasOwnProperty(type)) throw new Error("unknown type: " + type);
      for (var t = this._[type], i = 0, n = t.length; i < n; ++i) t[i].value.apply(that, args);
    }
  };

  function get$1(type, name) {
    for (var i = 0, n = type.length, c; i < n; ++i) {
      if ((c = type[i]).name === name) {
        return c.value;
      }
    }
  }

  function set$1(type, name, callback) {
    for (var i = 0, n = type.length; i < n; ++i) {
      if (type[i].name === name) {
        type[i] = noop, type = type.slice(0, i).concat(type.slice(i + 1));
        break;
      }
    }
    if (callback != null) type.push({name: name, value: callback});
    return type;
  }

  var xhtml = "http://www.w3.org/1999/xhtml";

  var namespaces = {
    svg: "http://www.w3.org/2000/svg",
    xhtml: xhtml,
    xlink: "http://www.w3.org/1999/xlink",
    xml: "http://www.w3.org/XML/1998/namespace",
    xmlns: "http://www.w3.org/2000/xmlns/"
  };

  function namespace(name) {
    var prefix = name += "", i = prefix.indexOf(":");
    if (i >= 0 && (prefix = name.slice(0, i)) !== "xmlns") name = name.slice(i + 1);
    return namespaces.hasOwnProperty(prefix) ? {space: namespaces[prefix], local: name} : name; // eslint-disable-line no-prototype-builtins
  }

  function creatorInherit(name) {
    return function() {
      var document = this.ownerDocument,
          uri = this.namespaceURI;
      return uri === xhtml && document.documentElement.namespaceURI === xhtml
          ? document.createElement(name)
          : document.createElementNS(uri, name);
    };
  }

  function creatorFixed(fullname) {
    return function() {
      return this.ownerDocument.createElementNS(fullname.space, fullname.local);
    };
  }

  function creator(name) {
    var fullname = namespace(name);
    return (fullname.local
        ? creatorFixed
        : creatorInherit)(fullname);
  }

  function none() {}

  function selector(selector) {
    return selector == null ? none : function() {
      return this.querySelector(selector);
    };
  }

  function selection_select(select) {
    if (typeof select !== "function") select = selector(select);

    for (var groups = this._groups, m = groups.length, subgroups = new Array(m), j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, subgroup = subgroups[j] = new Array(n), node, subnode, i = 0; i < n; ++i) {
        if ((node = group[i]) && (subnode = select.call(node, node.__data__, i, group))) {
          if ("__data__" in node) subnode.__data__ = node.__data__;
          subgroup[i] = subnode;
        }
      }
    }

    return new Selection$1(subgroups, this._parents);
  }

  // Given something array like (or null), returns something that is strictly an
  // array. This is used to ensure that array-like objects passed to d3.selectAll
  // or selection.selectAll are converted into proper arrays when creating a
  // selection; we don’t ever want to create a selection backed by a live
  // HTMLCollection or NodeList. However, note that selection.selectAll will use a
  // static NodeList as a group, since it safely derived from querySelectorAll.
  function array(x) {
    return x == null ? [] : Array.isArray(x) ? x : Array.from(x);
  }

  function empty() {
    return [];
  }

  function selectorAll(selector) {
    return selector == null ? empty : function() {
      return this.querySelectorAll(selector);
    };
  }

  function arrayAll(select) {
    return function() {
      return array(select.apply(this, arguments));
    };
  }

  function selection_selectAll(select) {
    if (typeof select === "function") select = arrayAll(select);
    else select = selectorAll(select);

    for (var groups = this._groups, m = groups.length, subgroups = [], parents = [], j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, node, i = 0; i < n; ++i) {
        if (node = group[i]) {
          subgroups.push(select.call(node, node.__data__, i, group));
          parents.push(node);
        }
      }
    }

    return new Selection$1(subgroups, parents);
  }

  function matcher(selector) {
    return function() {
      return this.matches(selector);
    };
  }

  function childMatcher(selector) {
    return function(node) {
      return node.matches(selector);
    };
  }

  var find = Array.prototype.find;

  function childFind(match) {
    return function() {
      return find.call(this.children, match);
    };
  }

  function childFirst() {
    return this.firstElementChild;
  }

  function selection_selectChild(match) {
    return this.select(match == null ? childFirst
        : childFind(typeof match === "function" ? match : childMatcher(match)));
  }

  var filter = Array.prototype.filter;

  function children() {
    return Array.from(this.children);
  }

  function childrenFilter(match) {
    return function() {
      return filter.call(this.children, match);
    };
  }

  function selection_selectChildren(match) {
    return this.selectAll(match == null ? children
        : childrenFilter(typeof match === "function" ? match : childMatcher(match)));
  }

  function selection_filter(match) {
    if (typeof match !== "function") match = matcher(match);

    for (var groups = this._groups, m = groups.length, subgroups = new Array(m), j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, subgroup = subgroups[j] = [], node, i = 0; i < n; ++i) {
        if ((node = group[i]) && match.call(node, node.__data__, i, group)) {
          subgroup.push(node);
        }
      }
    }

    return new Selection$1(subgroups, this._parents);
  }

  function sparse(update) {
    return new Array(update.length);
  }

  function selection_enter() {
    return new Selection$1(this._enter || this._groups.map(sparse), this._parents);
  }

  function EnterNode(parent, datum) {
    this.ownerDocument = parent.ownerDocument;
    this.namespaceURI = parent.namespaceURI;
    this._next = null;
    this._parent = parent;
    this.__data__ = datum;
  }

  EnterNode.prototype = {
    constructor: EnterNode,
    appendChild: function(child) { return this._parent.insertBefore(child, this._next); },
    insertBefore: function(child, next) { return this._parent.insertBefore(child, next); },
    querySelector: function(selector) { return this._parent.querySelector(selector); },
    querySelectorAll: function(selector) { return this._parent.querySelectorAll(selector); }
  };

  function constant$3(x) {
    return function() {
      return x;
    };
  }

  function bindIndex(parent, group, enter, update, exit, data) {
    var i = 0,
        node,
        groupLength = group.length,
        dataLength = data.length;

    // Put any non-null nodes that fit into update.
    // Put any null nodes into enter.
    // Put any remaining data into enter.
    for (; i < dataLength; ++i) {
      if (node = group[i]) {
        node.__data__ = data[i];
        update[i] = node;
      } else {
        enter[i] = new EnterNode(parent, data[i]);
      }
    }

    // Put any non-null nodes that don’t fit into exit.
    for (; i < groupLength; ++i) {
      if (node = group[i]) {
        exit[i] = node;
      }
    }
  }

  function bindKey(parent, group, enter, update, exit, data, key) {
    var i,
        node,
        nodeByKeyValue = new Map,
        groupLength = group.length,
        dataLength = data.length,
        keyValues = new Array(groupLength),
        keyValue;

    // Compute the key for each node.
    // If multiple nodes have the same key, the duplicates are added to exit.
    for (i = 0; i < groupLength; ++i) {
      if (node = group[i]) {
        keyValues[i] = keyValue = key.call(node, node.__data__, i, group) + "";
        if (nodeByKeyValue.has(keyValue)) {
          exit[i] = node;
        } else {
          nodeByKeyValue.set(keyValue, node);
        }
      }
    }

    // Compute the key for each datum.
    // If there a node associated with this key, join and add it to update.
    // If there is not (or the key is a duplicate), add it to enter.
    for (i = 0; i < dataLength; ++i) {
      keyValue = key.call(parent, data[i], i, data) + "";
      if (node = nodeByKeyValue.get(keyValue)) {
        update[i] = node;
        node.__data__ = data[i];
        nodeByKeyValue.delete(keyValue);
      } else {
        enter[i] = new EnterNode(parent, data[i]);
      }
    }

    // Add any remaining nodes that were not bound to data to exit.
    for (i = 0; i < groupLength; ++i) {
      if ((node = group[i]) && (nodeByKeyValue.get(keyValues[i]) === node)) {
        exit[i] = node;
      }
    }
  }

  function datum(node) {
    return node.__data__;
  }

  function selection_data(value, key) {
    if (!arguments.length) return Array.from(this, datum);

    var bind = key ? bindKey : bindIndex,
        parents = this._parents,
        groups = this._groups;

    if (typeof value !== "function") value = constant$3(value);

    for (var m = groups.length, update = new Array(m), enter = new Array(m), exit = new Array(m), j = 0; j < m; ++j) {
      var parent = parents[j],
          group = groups[j],
          groupLength = group.length,
          data = arraylike(value.call(parent, parent && parent.__data__, j, parents)),
          dataLength = data.length,
          enterGroup = enter[j] = new Array(dataLength),
          updateGroup = update[j] = new Array(dataLength),
          exitGroup = exit[j] = new Array(groupLength);

      bind(parent, group, enterGroup, updateGroup, exitGroup, data, key);

      // Now connect the enter nodes to their following update node, such that
      // appendChild can insert the materialized enter node before this node,
      // rather than at the end of the parent node.
      for (var i0 = 0, i1 = 0, previous, next; i0 < dataLength; ++i0) {
        if (previous = enterGroup[i0]) {
          if (i0 >= i1) i1 = i0 + 1;
          while (!(next = updateGroup[i1]) && ++i1 < dataLength);
          previous._next = next || null;
        }
      }
    }

    update = new Selection$1(update, parents);
    update._enter = enter;
    update._exit = exit;
    return update;
  }

  // Given some data, this returns an array-like view of it: an object that
  // exposes a length property and allows numeric indexing. Note that unlike
  // selectAll, this isn’t worried about “live” collections because the resulting
  // array will only be used briefly while data is being bound. (It is possible to
  // cause the data to change while iterating by using a key function, but please
  // don’t; we’d rather avoid a gratuitous copy.)
  function arraylike(data) {
    return typeof data === "object" && "length" in data
      ? data // Array, TypedArray, NodeList, array-like
      : Array.from(data); // Map, Set, iterable, string, or anything else
  }

  function selection_exit() {
    return new Selection$1(this._exit || this._groups.map(sparse), this._parents);
  }

  function selection_join(onenter, onupdate, onexit) {
    var enter = this.enter(), update = this, exit = this.exit();
    if (typeof onenter === "function") {
      enter = onenter(enter);
      if (enter) enter = enter.selection();
    } else {
      enter = enter.append(onenter + "");
    }
    if (onupdate != null) {
      update = onupdate(update);
      if (update) update = update.selection();
    }
    if (onexit == null) exit.remove(); else onexit(exit);
    return enter && update ? enter.merge(update).order() : update;
  }

  function selection_merge(context) {
    var selection = context.selection ? context.selection() : context;

    for (var groups0 = this._groups, groups1 = selection._groups, m0 = groups0.length, m1 = groups1.length, m = Math.min(m0, m1), merges = new Array(m0), j = 0; j < m; ++j) {
      for (var group0 = groups0[j], group1 = groups1[j], n = group0.length, merge = merges[j] = new Array(n), node, i = 0; i < n; ++i) {
        if (node = group0[i] || group1[i]) {
          merge[i] = node;
        }
      }
    }

    for (; j < m0; ++j) {
      merges[j] = groups0[j];
    }

    return new Selection$1(merges, this._parents);
  }

  function selection_order() {

    for (var groups = this._groups, j = -1, m = groups.length; ++j < m;) {
      for (var group = groups[j], i = group.length - 1, next = group[i], node; --i >= 0;) {
        if (node = group[i]) {
          if (next && node.compareDocumentPosition(next) ^ 4) next.parentNode.insertBefore(node, next);
          next = node;
        }
      }
    }

    return this;
  }

  function selection_sort(compare) {
    if (!compare) compare = ascending;

    function compareNode(a, b) {
      return a && b ? compare(a.__data__, b.__data__) : !a - !b;
    }

    for (var groups = this._groups, m = groups.length, sortgroups = new Array(m), j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, sortgroup = sortgroups[j] = new Array(n), node, i = 0; i < n; ++i) {
        if (node = group[i]) {
          sortgroup[i] = node;
        }
      }
      sortgroup.sort(compareNode);
    }

    return new Selection$1(sortgroups, this._parents).order();
  }

  function ascending(a, b) {
    return a < b ? -1 : a > b ? 1 : a >= b ? 0 : NaN;
  }

  function selection_call() {
    var callback = arguments[0];
    arguments[0] = this;
    callback.apply(null, arguments);
    return this;
  }

  function selection_nodes() {
    return Array.from(this);
  }

  function selection_node() {

    for (var groups = this._groups, j = 0, m = groups.length; j < m; ++j) {
      for (var group = groups[j], i = 0, n = group.length; i < n; ++i) {
        var node = group[i];
        if (node) return node;
      }
    }

    return null;
  }

  function selection_size() {
    let size = 0;
    for (const node of this) ++size; // eslint-disable-line no-unused-vars
    return size;
  }

  function selection_empty() {
    return !this.node();
  }

  function selection_each(callback) {

    for (var groups = this._groups, j = 0, m = groups.length; j < m; ++j) {
      for (var group = groups[j], i = 0, n = group.length, node; i < n; ++i) {
        if (node = group[i]) callback.call(node, node.__data__, i, group);
      }
    }

    return this;
  }

  function attrRemove$1(name) {
    return function() {
      this.removeAttribute(name);
    };
  }

  function attrRemoveNS$1(fullname) {
    return function() {
      this.removeAttributeNS(fullname.space, fullname.local);
    };
  }

  function attrConstant$1(name, value) {
    return function() {
      this.setAttribute(name, value);
    };
  }

  function attrConstantNS$1(fullname, value) {
    return function() {
      this.setAttributeNS(fullname.space, fullname.local, value);
    };
  }

  function attrFunction$1(name, value) {
    return function() {
      var v = value.apply(this, arguments);
      if (v == null) this.removeAttribute(name);
      else this.setAttribute(name, v);
    };
  }

  function attrFunctionNS$1(fullname, value) {
    return function() {
      var v = value.apply(this, arguments);
      if (v == null) this.removeAttributeNS(fullname.space, fullname.local);
      else this.setAttributeNS(fullname.space, fullname.local, v);
    };
  }

  function selection_attr(name, value) {
    var fullname = namespace(name);

    if (arguments.length < 2) {
      var node = this.node();
      return fullname.local
          ? node.getAttributeNS(fullname.space, fullname.local)
          : node.getAttribute(fullname);
    }

    return this.each((value == null
        ? (fullname.local ? attrRemoveNS$1 : attrRemove$1) : (typeof value === "function"
        ? (fullname.local ? attrFunctionNS$1 : attrFunction$1)
        : (fullname.local ? attrConstantNS$1 : attrConstant$1)))(fullname, value));
  }

  function defaultView(node) {
    return (node.ownerDocument && node.ownerDocument.defaultView) // node is a Node
        || (node.document && node) // node is a Window
        || node.defaultView; // node is a Document
  }

  function styleRemove$1(name) {
    return function() {
      this.style.removeProperty(name);
    };
  }

  function styleConstant$1(name, value, priority) {
    return function() {
      this.style.setProperty(name, value, priority);
    };
  }

  function styleFunction$1(name, value, priority) {
    return function() {
      var v = value.apply(this, arguments);
      if (v == null) this.style.removeProperty(name);
      else this.style.setProperty(name, v, priority);
    };
  }

  function selection_style(name, value, priority) {
    return arguments.length > 1
        ? this.each((value == null
              ? styleRemove$1 : typeof value === "function"
              ? styleFunction$1
              : styleConstant$1)(name, value, priority == null ? "" : priority))
        : styleValue(this.node(), name);
  }

  function styleValue(node, name) {
    return node.style.getPropertyValue(name)
        || defaultView(node).getComputedStyle(node, null).getPropertyValue(name);
  }

  function propertyRemove(name) {
    return function() {
      delete this[name];
    };
  }

  function propertyConstant(name, value) {
    return function() {
      this[name] = value;
    };
  }

  function propertyFunction(name, value) {
    return function() {
      var v = value.apply(this, arguments);
      if (v == null) delete this[name];
      else this[name] = v;
    };
  }

  function selection_property(name, value) {
    return arguments.length > 1
        ? this.each((value == null
            ? propertyRemove : typeof value === "function"
            ? propertyFunction
            : propertyConstant)(name, value))
        : this.node()[name];
  }

  function classArray(string) {
    return string.trim().split(/^|\s+/);
  }

  function classList(node) {
    return node.classList || new ClassList(node);
  }

  function ClassList(node) {
    this._node = node;
    this._names = classArray(node.getAttribute("class") || "");
  }

  ClassList.prototype = {
    add: function(name) {
      var i = this._names.indexOf(name);
      if (i < 0) {
        this._names.push(name);
        this._node.setAttribute("class", this._names.join(" "));
      }
    },
    remove: function(name) {
      var i = this._names.indexOf(name);
      if (i >= 0) {
        this._names.splice(i, 1);
        this._node.setAttribute("class", this._names.join(" "));
      }
    },
    contains: function(name) {
      return this._names.indexOf(name) >= 0;
    }
  };

  function classedAdd(node, names) {
    var list = classList(node), i = -1, n = names.length;
    while (++i < n) list.add(names[i]);
  }

  function classedRemove(node, names) {
    var list = classList(node), i = -1, n = names.length;
    while (++i < n) list.remove(names[i]);
  }

  function classedTrue(names) {
    return function() {
      classedAdd(this, names);
    };
  }

  function classedFalse(names) {
    return function() {
      classedRemove(this, names);
    };
  }

  function classedFunction(names, value) {
    return function() {
      (value.apply(this, arguments) ? classedAdd : classedRemove)(this, names);
    };
  }

  function selection_classed(name, value) {
    var names = classArray(name + "");

    if (arguments.length < 2) {
      var list = classList(this.node()), i = -1, n = names.length;
      while (++i < n) if (!list.contains(names[i])) return false;
      return true;
    }

    return this.each((typeof value === "function"
        ? classedFunction : value
        ? classedTrue
        : classedFalse)(names, value));
  }

  function textRemove() {
    this.textContent = "";
  }

  function textConstant$1(value) {
    return function() {
      this.textContent = value;
    };
  }

  function textFunction$1(value) {
    return function() {
      var v = value.apply(this, arguments);
      this.textContent = v == null ? "" : v;
    };
  }

  function selection_text(value) {
    return arguments.length
        ? this.each(value == null
            ? textRemove : (typeof value === "function"
            ? textFunction$1
            : textConstant$1)(value))
        : this.node().textContent;
  }

  function htmlRemove() {
    this.innerHTML = "";
  }

  function htmlConstant(value) {
    return function() {
      this.innerHTML = value;
    };
  }

  function htmlFunction(value) {
    return function() {
      var v = value.apply(this, arguments);
      this.innerHTML = v == null ? "" : v;
    };
  }

  function selection_html(value) {
    return arguments.length
        ? this.each(value == null
            ? htmlRemove : (typeof value === "function"
            ? htmlFunction
            : htmlConstant)(value))
        : this.node().innerHTML;
  }

  function raise() {
    if (this.nextSibling) this.parentNode.appendChild(this);
  }

  function selection_raise() {
    return this.each(raise);
  }

  function lower() {
    if (this.previousSibling) this.parentNode.insertBefore(this, this.parentNode.firstChild);
  }

  function selection_lower() {
    return this.each(lower);
  }

  function selection_append(name) {
    var create = typeof name === "function" ? name : creator(name);
    return this.select(function() {
      return this.appendChild(create.apply(this, arguments));
    });
  }

  function constantNull() {
    return null;
  }

  function selection_insert(name, before) {
    var create = typeof name === "function" ? name : creator(name),
        select = before == null ? constantNull : typeof before === "function" ? before : selector(before);
    return this.select(function() {
      return this.insertBefore(create.apply(this, arguments), select.apply(this, arguments) || null);
    });
  }

  function remove() {
    var parent = this.parentNode;
    if (parent) parent.removeChild(this);
  }

  function selection_remove() {
    return this.each(remove);
  }

  function selection_cloneShallow() {
    var clone = this.cloneNode(false), parent = this.parentNode;
    return parent ? parent.insertBefore(clone, this.nextSibling) : clone;
  }

  function selection_cloneDeep() {
    var clone = this.cloneNode(true), parent = this.parentNode;
    return parent ? parent.insertBefore(clone, this.nextSibling) : clone;
  }

  function selection_clone(deep) {
    return this.select(deep ? selection_cloneDeep : selection_cloneShallow);
  }

  function selection_datum(value) {
    return arguments.length
        ? this.property("__data__", value)
        : this.node().__data__;
  }

  function contextListener(listener) {
    return function(event) {
      listener.call(this, event, this.__data__);
    };
  }

  function parseTypenames(typenames) {
    return typenames.trim().split(/^|\s+/).map(function(t) {
      var name = "", i = t.indexOf(".");
      if (i >= 0) name = t.slice(i + 1), t = t.slice(0, i);
      return {type: t, name: name};
    });
  }

  function onRemove(typename) {
    return function() {
      var on = this.__on;
      if (!on) return;
      for (var j = 0, i = -1, m = on.length, o; j < m; ++j) {
        if (o = on[j], (!typename.type || o.type === typename.type) && o.name === typename.name) {
          this.removeEventListener(o.type, o.listener, o.options);
        } else {
          on[++i] = o;
        }
      }
      if (++i) on.length = i;
      else delete this.__on;
    };
  }

  function onAdd(typename, value, options) {
    return function() {
      var on = this.__on, o, listener = contextListener(value);
      if (on) for (var j = 0, m = on.length; j < m; ++j) {
        if ((o = on[j]).type === typename.type && o.name === typename.name) {
          this.removeEventListener(o.type, o.listener, o.options);
          this.addEventListener(o.type, o.listener = listener, o.options = options);
          o.value = value;
          return;
        }
      }
      this.addEventListener(typename.type, listener, options);
      o = {type: typename.type, name: typename.name, value: value, listener: listener, options: options};
      if (!on) this.__on = [o];
      else on.push(o);
    };
  }

  function selection_on(typename, value, options) {
    var typenames = parseTypenames(typename + ""), i, n = typenames.length, t;

    if (arguments.length < 2) {
      var on = this.node().__on;
      if (on) for (var j = 0, m = on.length, o; j < m; ++j) {
        for (i = 0, o = on[j]; i < n; ++i) {
          if ((t = typenames[i]).type === o.type && t.name === o.name) {
            return o.value;
          }
        }
      }
      return;
    }

    on = value ? onAdd : onRemove;
    for (i = 0; i < n; ++i) this.each(on(typenames[i], value, options));
    return this;
  }

  function dispatchEvent(node, type, params) {
    var window = defaultView(node),
        event = window.CustomEvent;

    if (typeof event === "function") {
      event = new event(type, params);
    } else {
      event = window.document.createEvent("Event");
      if (params) event.initEvent(type, params.bubbles, params.cancelable), event.detail = params.detail;
      else event.initEvent(type, false, false);
    }

    node.dispatchEvent(event);
  }

  function dispatchConstant(type, params) {
    return function() {
      return dispatchEvent(this, type, params);
    };
  }

  function dispatchFunction(type, params) {
    return function() {
      return dispatchEvent(this, type, params.apply(this, arguments));
    };
  }

  function selection_dispatch(type, params) {
    return this.each((typeof params === "function"
        ? dispatchFunction
        : dispatchConstant)(type, params));
  }

  function* selection_iterator() {
    for (var groups = this._groups, j = 0, m = groups.length; j < m; ++j) {
      for (var group = groups[j], i = 0, n = group.length, node; i < n; ++i) {
        if (node = group[i]) yield node;
      }
    }
  }

  var root = [null];

  function Selection$1(groups, parents) {
    this._groups = groups;
    this._parents = parents;
  }

  function selection() {
    return new Selection$1([[document.documentElement]], root);
  }

  function selection_selection() {
    return this;
  }

  Selection$1.prototype = selection.prototype = {
    constructor: Selection$1,
    select: selection_select,
    selectAll: selection_selectAll,
    selectChild: selection_selectChild,
    selectChildren: selection_selectChildren,
    filter: selection_filter,
    data: selection_data,
    enter: selection_enter,
    exit: selection_exit,
    join: selection_join,
    merge: selection_merge,
    selection: selection_selection,
    order: selection_order,
    sort: selection_sort,
    call: selection_call,
    nodes: selection_nodes,
    node: selection_node,
    size: selection_size,
    empty: selection_empty,
    each: selection_each,
    attr: selection_attr,
    style: selection_style,
    property: selection_property,
    classed: selection_classed,
    text: selection_text,
    html: selection_html,
    raise: selection_raise,
    lower: selection_lower,
    append: selection_append,
    insert: selection_insert,
    remove: selection_remove,
    clone: selection_clone,
    datum: selection_datum,
    on: selection_on,
    dispatch: selection_dispatch,
    [Symbol.iterator]: selection_iterator
  };

  function select(selector) {
    return typeof selector === "string"
        ? new Selection$1([[document.querySelector(selector)]], [document.documentElement])
        : new Selection$1([[selector]], root);
  }

  function define(constructor, factory, prototype) {
    constructor.prototype = factory.prototype = prototype;
    prototype.constructor = constructor;
  }

  function extend(parent, definition) {
    var prototype = Object.create(parent.prototype);
    for (var key in definition) prototype[key] = definition[key];
    return prototype;
  }

  function Color() {}

  var darker = 0.7;
  var brighter = 1 / darker;

  var reI = "\\s*([+-]?\\d+)\\s*",
      reN = "\\s*([+-]?\\d*\\.?\\d+(?:[eE][+-]?\\d+)?)\\s*",
      reP = "\\s*([+-]?\\d*\\.?\\d+(?:[eE][+-]?\\d+)?)%\\s*",
      reHex = /^#([0-9a-f]{3,8})$/,
      reRgbInteger = new RegExp("^rgb\\(" + [reI, reI, reI] + "\\)$"),
      reRgbPercent = new RegExp("^rgb\\(" + [reP, reP, reP] + "\\)$"),
      reRgbaInteger = new RegExp("^rgba\\(" + [reI, reI, reI, reN] + "\\)$"),
      reRgbaPercent = new RegExp("^rgba\\(" + [reP, reP, reP, reN] + "\\)$"),
      reHslPercent = new RegExp("^hsl\\(" + [reN, reP, reP] + "\\)$"),
      reHslaPercent = new RegExp("^hsla\\(" + [reN, reP, reP, reN] + "\\)$");

  var named = {
    aliceblue: 0xf0f8ff,
    antiquewhite: 0xfaebd7,
    aqua: 0x00ffff,
    aquamarine: 0x7fffd4,
    azure: 0xf0ffff,
    beige: 0xf5f5dc,
    bisque: 0xffe4c4,
    black: 0x000000,
    blanchedalmond: 0xffebcd,
    blue: 0x0000ff,
    blueviolet: 0x8a2be2,
    brown: 0xa52a2a,
    burlywood: 0xdeb887,
    cadetblue: 0x5f9ea0,
    chartreuse: 0x7fff00,
    chocolate: 0xd2691e,
    coral: 0xff7f50,
    cornflowerblue: 0x6495ed,
    cornsilk: 0xfff8dc,
    crimson: 0xdc143c,
    cyan: 0x00ffff,
    darkblue: 0x00008b,
    darkcyan: 0x008b8b,
    darkgoldenrod: 0xb8860b,
    darkgray: 0xa9a9a9,
    darkgreen: 0x006400,
    darkgrey: 0xa9a9a9,
    darkkhaki: 0xbdb76b,
    darkmagenta: 0x8b008b,
    darkolivegreen: 0x556b2f,
    darkorange: 0xff8c00,
    darkorchid: 0x9932cc,
    darkred: 0x8b0000,
    darksalmon: 0xe9967a,
    darkseagreen: 0x8fbc8f,
    darkslateblue: 0x483d8b,
    darkslategray: 0x2f4f4f,
    darkslategrey: 0x2f4f4f,
    darkturquoise: 0x00ced1,
    darkviolet: 0x9400d3,
    deeppink: 0xff1493,
    deepskyblue: 0x00bfff,
    dimgray: 0x696969,
    dimgrey: 0x696969,
    dodgerblue: 0x1e90ff,
    firebrick: 0xb22222,
    floralwhite: 0xfffaf0,
    forestgreen: 0x228b22,
    fuchsia: 0xff00ff,
    gainsboro: 0xdcdcdc,
    ghostwhite: 0xf8f8ff,
    gold: 0xffd700,
    goldenrod: 0xdaa520,
    gray: 0x808080,
    green: 0x008000,
    greenyellow: 0xadff2f,
    grey: 0x808080,
    honeydew: 0xf0fff0,
    hotpink: 0xff69b4,
    indianred: 0xcd5c5c,
    indigo: 0x4b0082,
    ivory: 0xfffff0,
    khaki: 0xf0e68c,
    lavender: 0xe6e6fa,
    lavenderblush: 0xfff0f5,
    lawngreen: 0x7cfc00,
    lemonchiffon: 0xfffacd,
    lightblue: 0xadd8e6,
    lightcoral: 0xf08080,
    lightcyan: 0xe0ffff,
    lightgoldenrodyellow: 0xfafad2,
    lightgray: 0xd3d3d3,
    lightgreen: 0x90ee90,
    lightgrey: 0xd3d3d3,
    lightpink: 0xffb6c1,
    lightsalmon: 0xffa07a,
    lightseagreen: 0x20b2aa,
    lightskyblue: 0x87cefa,
    lightslategray: 0x778899,
    lightslategrey: 0x778899,
    lightsteelblue: 0xb0c4de,
    lightyellow: 0xffffe0,
    lime: 0x00ff00,
    limegreen: 0x32cd32,
    linen: 0xfaf0e6,
    magenta: 0xff00ff,
    maroon: 0x800000,
    mediumaquamarine: 0x66cdaa,
    mediumblue: 0x0000cd,
    mediumorchid: 0xba55d3,
    mediumpurple: 0x9370db,
    mediumseagreen: 0x3cb371,
    mediumslateblue: 0x7b68ee,
    mediumspringgreen: 0x00fa9a,
    mediumturquoise: 0x48d1cc,
    mediumvioletred: 0xc71585,
    midnightblue: 0x191970,
    mintcream: 0xf5fffa,
    mistyrose: 0xffe4e1,
    moccasin: 0xffe4b5,
    navajowhite: 0xffdead,
    navy: 0x000080,
    oldlace: 0xfdf5e6,
    olive: 0x808000,
    olivedrab: 0x6b8e23,
    orange: 0xffa500,
    orangered: 0xff4500,
    orchid: 0xda70d6,
    palegoldenrod: 0xeee8aa,
    palegreen: 0x98fb98,
    paleturquoise: 0xafeeee,
    palevioletred: 0xdb7093,
    papayawhip: 0xffefd5,
    peachpuff: 0xffdab9,
    peru: 0xcd853f,
    pink: 0xffc0cb,
    plum: 0xdda0dd,
    powderblue: 0xb0e0e6,
    purple: 0x800080,
    rebeccapurple: 0x663399,
    red: 0xff0000,
    rosybrown: 0xbc8f8f,
    royalblue: 0x4169e1,
    saddlebrown: 0x8b4513,
    salmon: 0xfa8072,
    sandybrown: 0xf4a460,
    seagreen: 0x2e8b57,
    seashell: 0xfff5ee,
    sienna: 0xa0522d,
    silver: 0xc0c0c0,
    skyblue: 0x87ceeb,
    slateblue: 0x6a5acd,
    slategray: 0x708090,
    slategrey: 0x708090,
    snow: 0xfffafa,
    springgreen: 0x00ff7f,
    steelblue: 0x4682b4,
    tan: 0xd2b48c,
    teal: 0x008080,
    thistle: 0xd8bfd8,
    tomato: 0xff6347,
    turquoise: 0x40e0d0,
    violet: 0xee82ee,
    wheat: 0xf5deb3,
    white: 0xffffff,
    whitesmoke: 0xf5f5f5,
    yellow: 0xffff00,
    yellowgreen: 0x9acd32
  };

  define(Color, color, {
    copy: function(channels) {
      return Object.assign(new this.constructor, this, channels);
    },
    displayable: function() {
      return this.rgb().displayable();
    },
    hex: color_formatHex, // Deprecated! Use color.formatHex.
    formatHex: color_formatHex,
    formatHsl: color_formatHsl,
    formatRgb: color_formatRgb,
    toString: color_formatRgb
  });

  function color_formatHex() {
    return this.rgb().formatHex();
  }

  function color_formatHsl() {
    return hslConvert(this).formatHsl();
  }

  function color_formatRgb() {
    return this.rgb().formatRgb();
  }

  function color(format) {
    var m, l;
    format = (format + "").trim().toLowerCase();
    return (m = reHex.exec(format)) ? (l = m[1].length, m = parseInt(m[1], 16), l === 6 ? rgbn(m) // #ff0000
        : l === 3 ? new Rgb((m >> 8 & 0xf) | (m >> 4 & 0xf0), (m >> 4 & 0xf) | (m & 0xf0), ((m & 0xf) << 4) | (m & 0xf), 1) // #f00
        : l === 8 ? rgba(m >> 24 & 0xff, m >> 16 & 0xff, m >> 8 & 0xff, (m & 0xff) / 0xff) // #ff000000
        : l === 4 ? rgba((m >> 12 & 0xf) | (m >> 8 & 0xf0), (m >> 8 & 0xf) | (m >> 4 & 0xf0), (m >> 4 & 0xf) | (m & 0xf0), (((m & 0xf) << 4) | (m & 0xf)) / 0xff) // #f000
        : null) // invalid hex
        : (m = reRgbInteger.exec(format)) ? new Rgb(m[1], m[2], m[3], 1) // rgb(255, 0, 0)
        : (m = reRgbPercent.exec(format)) ? new Rgb(m[1] * 255 / 100, m[2] * 255 / 100, m[3] * 255 / 100, 1) // rgb(100%, 0%, 0%)
        : (m = reRgbaInteger.exec(format)) ? rgba(m[1], m[2], m[3], m[4]) // rgba(255, 0, 0, 1)
        : (m = reRgbaPercent.exec(format)) ? rgba(m[1] * 255 / 100, m[2] * 255 / 100, m[3] * 255 / 100, m[4]) // rgb(100%, 0%, 0%, 1)
        : (m = reHslPercent.exec(format)) ? hsla(m[1], m[2] / 100, m[3] / 100, 1) // hsl(120, 50%, 50%)
        : (m = reHslaPercent.exec(format)) ? hsla(m[1], m[2] / 100, m[3] / 100, m[4]) // hsla(120, 50%, 50%, 1)
        : named.hasOwnProperty(format) ? rgbn(named[format]) // eslint-disable-line no-prototype-builtins
        : format === "transparent" ? new Rgb(NaN, NaN, NaN, 0)
        : null;
  }

  function rgbn(n) {
    return new Rgb(n >> 16 & 0xff, n >> 8 & 0xff, n & 0xff, 1);
  }

  function rgba(r, g, b, a) {
    if (a <= 0) r = g = b = NaN;
    return new Rgb(r, g, b, a);
  }

  function rgbConvert(o) {
    if (!(o instanceof Color)) o = color(o);
    if (!o) return new Rgb;
    o = o.rgb();
    return new Rgb(o.r, o.g, o.b, o.opacity);
  }

  function rgb(r, g, b, opacity) {
    return arguments.length === 1 ? rgbConvert(r) : new Rgb(r, g, b, opacity == null ? 1 : opacity);
  }

  function Rgb(r, g, b, opacity) {
    this.r = +r;
    this.g = +g;
    this.b = +b;
    this.opacity = +opacity;
  }

  define(Rgb, rgb, extend(Color, {
    brighter: function(k) {
      k = k == null ? brighter : Math.pow(brighter, k);
      return new Rgb(this.r * k, this.g * k, this.b * k, this.opacity);
    },
    darker: function(k) {
      k = k == null ? darker : Math.pow(darker, k);
      return new Rgb(this.r * k, this.g * k, this.b * k, this.opacity);
    },
    rgb: function() {
      return this;
    },
    displayable: function() {
      return (-0.5 <= this.r && this.r < 255.5)
          && (-0.5 <= this.g && this.g < 255.5)
          && (-0.5 <= this.b && this.b < 255.5)
          && (0 <= this.opacity && this.opacity <= 1);
    },
    hex: rgb_formatHex, // Deprecated! Use color.formatHex.
    formatHex: rgb_formatHex,
    formatRgb: rgb_formatRgb,
    toString: rgb_formatRgb
  }));

  function rgb_formatHex() {
    return "#" + hex(this.r) + hex(this.g) + hex(this.b);
  }

  function rgb_formatRgb() {
    var a = this.opacity; a = isNaN(a) ? 1 : Math.max(0, Math.min(1, a));
    return (a === 1 ? "rgb(" : "rgba(")
        + Math.max(0, Math.min(255, Math.round(this.r) || 0)) + ", "
        + Math.max(0, Math.min(255, Math.round(this.g) || 0)) + ", "
        + Math.max(0, Math.min(255, Math.round(this.b) || 0))
        + (a === 1 ? ")" : ", " + a + ")");
  }

  function hex(value) {
    value = Math.max(0, Math.min(255, Math.round(value) || 0));
    return (value < 16 ? "0" : "") + value.toString(16);
  }

  function hsla(h, s, l, a) {
    if (a <= 0) h = s = l = NaN;
    else if (l <= 0 || l >= 1) h = s = NaN;
    else if (s <= 0) h = NaN;
    return new Hsl(h, s, l, a);
  }

  function hslConvert(o) {
    if (o instanceof Hsl) return new Hsl(o.h, o.s, o.l, o.opacity);
    if (!(o instanceof Color)) o = color(o);
    if (!o) return new Hsl;
    if (o instanceof Hsl) return o;
    o = o.rgb();
    var r = o.r / 255,
        g = o.g / 255,
        b = o.b / 255,
        min = Math.min(r, g, b),
        max = Math.max(r, g, b),
        h = NaN,
        s = max - min,
        l = (max + min) / 2;
    if (s) {
      if (r === max) h = (g - b) / s + (g < b) * 6;
      else if (g === max) h = (b - r) / s + 2;
      else h = (r - g) / s + 4;
      s /= l < 0.5 ? max + min : 2 - max - min;
      h *= 60;
    } else {
      s = l > 0 && l < 1 ? 0 : h;
    }
    return new Hsl(h, s, l, o.opacity);
  }

  function hsl(h, s, l, opacity) {
    return arguments.length === 1 ? hslConvert(h) : new Hsl(h, s, l, opacity == null ? 1 : opacity);
  }

  function Hsl(h, s, l, opacity) {
    this.h = +h;
    this.s = +s;
    this.l = +l;
    this.opacity = +opacity;
  }

  define(Hsl, hsl, extend(Color, {
    brighter: function(k) {
      k = k == null ? brighter : Math.pow(brighter, k);
      return new Hsl(this.h, this.s, this.l * k, this.opacity);
    },
    darker: function(k) {
      k = k == null ? darker : Math.pow(darker, k);
      return new Hsl(this.h, this.s, this.l * k, this.opacity);
    },
    rgb: function() {
      var h = this.h % 360 + (this.h < 0) * 360,
          s = isNaN(h) || isNaN(this.s) ? 0 : this.s,
          l = this.l,
          m2 = l + (l < 0.5 ? l : 1 - l) * s,
          m1 = 2 * l - m2;
      return new Rgb(
        hsl2rgb(h >= 240 ? h - 240 : h + 120, m1, m2),
        hsl2rgb(h, m1, m2),
        hsl2rgb(h < 120 ? h + 240 : h - 120, m1, m2),
        this.opacity
      );
    },
    displayable: function() {
      return (0 <= this.s && this.s <= 1 || isNaN(this.s))
          && (0 <= this.l && this.l <= 1)
          && (0 <= this.opacity && this.opacity <= 1);
    },
    formatHsl: function() {
      var a = this.opacity; a = isNaN(a) ? 1 : Math.max(0, Math.min(1, a));
      return (a === 1 ? "hsl(" : "hsla(")
          + (this.h || 0) + ", "
          + (this.s || 0) * 100 + "%, "
          + (this.l || 0) * 100 + "%"
          + (a === 1 ? ")" : ", " + a + ")");
    }
  }));

  /* From FvD 13.37, CSS Color Module Level 3 */
  function hsl2rgb(h, m1, m2) {
    return (h < 60 ? m1 + (m2 - m1) * h / 60
        : h < 180 ? m2
        : h < 240 ? m1 + (m2 - m1) * (240 - h) / 60
        : m1) * 255;
  }

  const radians = Math.PI / 180;
  const degrees$1 = 180 / Math.PI;

  var A = -0.14861,
      B = +1.78277,
      C = -0.29227,
      D = -0.90649,
      E = +1.97294,
      ED = E * D,
      EB = E * B,
      BC_DA = B * C - D * A;

  function cubehelixConvert(o) {
    if (o instanceof Cubehelix) return new Cubehelix(o.h, o.s, o.l, o.opacity);
    if (!(o instanceof Rgb)) o = rgbConvert(o);
    var r = o.r / 255,
        g = o.g / 255,
        b = o.b / 255,
        l = (BC_DA * b + ED * r - EB * g) / (BC_DA + ED - EB),
        bl = b - l,
        k = (E * (g - l) - C * bl) / D,
        s = Math.sqrt(k * k + bl * bl) / (E * l * (1 - l)), // NaN if l=0 or l=1
        h = s ? Math.atan2(k, bl) * degrees$1 - 120 : NaN;
    return new Cubehelix(h < 0 ? h + 360 : h, s, l, o.opacity);
  }

  function cubehelix$1(h, s, l, opacity) {
    return arguments.length === 1 ? cubehelixConvert(h) : new Cubehelix(h, s, l, opacity == null ? 1 : opacity);
  }

  function Cubehelix(h, s, l, opacity) {
    this.h = +h;
    this.s = +s;
    this.l = +l;
    this.opacity = +opacity;
  }

  define(Cubehelix, cubehelix$1, extend(Color, {
    brighter: function(k) {
      k = k == null ? brighter : Math.pow(brighter, k);
      return new Cubehelix(this.h, this.s, this.l * k, this.opacity);
    },
    darker: function(k) {
      k = k == null ? darker : Math.pow(darker, k);
      return new Cubehelix(this.h, this.s, this.l * k, this.opacity);
    },
    rgb: function() {
      var h = isNaN(this.h) ? 0 : (this.h + 120) * radians,
          l = +this.l,
          a = isNaN(this.s) ? 0 : this.s * l * (1 - l),
          cosh = Math.cos(h),
          sinh = Math.sin(h);
      return new Rgb(
        255 * (l + a * (A * cosh + B * sinh)),
        255 * (l + a * (C * cosh + D * sinh)),
        255 * (l + a * (E * cosh)),
        this.opacity
      );
    }
  }));

  var constant$2 = x => () => x;

  function linear(a, d) {
    return function(t) {
      return a + t * d;
    };
  }

  function exponential(a, b, y) {
    return a = Math.pow(a, y), b = Math.pow(b, y) - a, y = 1 / y, function(t) {
      return Math.pow(a + t * b, y);
    };
  }

  function hue(a, b) {
    var d = b - a;
    return d ? linear(a, d > 180 || d < -180 ? d - 360 * Math.round(d / 360) : d) : constant$2(isNaN(a) ? b : a);
  }

  function gamma(y) {
    return (y = +y) === 1 ? nogamma : function(a, b) {
      return b - a ? exponential(a, b, y) : constant$2(isNaN(a) ? b : a);
    };
  }

  function nogamma(a, b) {
    var d = b - a;
    return d ? linear(a, d) : constant$2(isNaN(a) ? b : a);
  }

  var interpolateRgb = (function rgbGamma(y) {
    var color = gamma(y);

    function rgb$1(start, end) {
      var r = color((start = rgb(start)).r, (end = rgb(end)).r),
          g = color(start.g, end.g),
          b = color(start.b, end.b),
          opacity = nogamma(start.opacity, end.opacity);
      return function(t) {
        start.r = r(t);
        start.g = g(t);
        start.b = b(t);
        start.opacity = opacity(t);
        return start + "";
      };
    }

    rgb$1.gamma = rgbGamma;

    return rgb$1;
  })(1);

  function interpolateNumber(a, b) {
    return a = +a, b = +b, function(t) {
      return a * (1 - t) + b * t;
    };
  }

  var reA = /[-+]?(?:\d+\.?\d*|\.?\d+)(?:[eE][-+]?\d+)?/g,
      reB = new RegExp(reA.source, "g");

  function zero(b) {
    return function() {
      return b;
    };
  }

  function one(b) {
    return function(t) {
      return b(t) + "";
    };
  }

  function interpolateString(a, b) {
    var bi = reA.lastIndex = reB.lastIndex = 0, // scan index for next number in b
        am, // current match in a
        bm, // current match in b
        bs, // string preceding current number in b, if any
        i = -1, // index in s
        s = [], // string constants and placeholders
        q = []; // number interpolators

    // Coerce inputs to strings.
    a = a + "", b = b + "";

    // Interpolate pairs of numbers in a & b.
    while ((am = reA.exec(a))
        && (bm = reB.exec(b))) {
      if ((bs = bm.index) > bi) { // a string precedes the next number in b
        bs = b.slice(bi, bs);
        if (s[i]) s[i] += bs; // coalesce with previous string
        else s[++i] = bs;
      }
      if ((am = am[0]) === (bm = bm[0])) { // numbers in a & b match
        if (s[i]) s[i] += bm; // coalesce with previous string
        else s[++i] = bm;
      } else { // interpolate non-matching numbers
        s[++i] = null;
        q.push({i: i, x: interpolateNumber(am, bm)});
      }
      bi = reB.lastIndex;
    }

    // Add remains of b.
    if (bi < b.length) {
      bs = b.slice(bi);
      if (s[i]) s[i] += bs; // coalesce with previous string
      else s[++i] = bs;
    }

    // Special optimization for only a single match.
    // Otherwise, interpolate each of the numbers and rejoin the string.
    return s.length < 2 ? (q[0]
        ? one(q[0].x)
        : zero(b))
        : (b = q.length, function(t) {
            for (var i = 0, o; i < b; ++i) s[(o = q[i]).i] = o.x(t);
            return s.join("");
          });
  }

  var degrees = 180 / Math.PI;

  var identity$1 = {
    translateX: 0,
    translateY: 0,
    rotate: 0,
    skewX: 0,
    scaleX: 1,
    scaleY: 1
  };

  function decompose(a, b, c, d, e, f) {
    var scaleX, scaleY, skewX;
    if (scaleX = Math.sqrt(a * a + b * b)) a /= scaleX, b /= scaleX;
    if (skewX = a * c + b * d) c -= a * skewX, d -= b * skewX;
    if (scaleY = Math.sqrt(c * c + d * d)) c /= scaleY, d /= scaleY, skewX /= scaleY;
    if (a * d < b * c) a = -a, b = -b, skewX = -skewX, scaleX = -scaleX;
    return {
      translateX: e,
      translateY: f,
      rotate: Math.atan2(b, a) * degrees,
      skewX: Math.atan(skewX) * degrees,
      scaleX: scaleX,
      scaleY: scaleY
    };
  }

  var svgNode;

  /* eslint-disable no-undef */
  function parseCss(value) {
    const m = new (typeof DOMMatrix === "function" ? DOMMatrix : WebKitCSSMatrix)(value + "");
    return m.isIdentity ? identity$1 : decompose(m.a, m.b, m.c, m.d, m.e, m.f);
  }

  function parseSvg(value) {
    if (value == null) return identity$1;
    if (!svgNode) svgNode = document.createElementNS("http://www.w3.org/2000/svg", "g");
    svgNode.setAttribute("transform", value);
    if (!(value = svgNode.transform.baseVal.consolidate())) return identity$1;
    value = value.matrix;
    return decompose(value.a, value.b, value.c, value.d, value.e, value.f);
  }

  function interpolateTransform(parse, pxComma, pxParen, degParen) {

    function pop(s) {
      return s.length ? s.pop() + " " : "";
    }

    function translate(xa, ya, xb, yb, s, q) {
      if (xa !== xb || ya !== yb) {
        var i = s.push("translate(", null, pxComma, null, pxParen);
        q.push({i: i - 4, x: interpolateNumber(xa, xb)}, {i: i - 2, x: interpolateNumber(ya, yb)});
      } else if (xb || yb) {
        s.push("translate(" + xb + pxComma + yb + pxParen);
      }
    }

    function rotate(a, b, s, q) {
      if (a !== b) {
        if (a - b > 180) b += 360; else if (b - a > 180) a += 360; // shortest path
        q.push({i: s.push(pop(s) + "rotate(", null, degParen) - 2, x: interpolateNumber(a, b)});
      } else if (b) {
        s.push(pop(s) + "rotate(" + b + degParen);
      }
    }

    function skewX(a, b, s, q) {
      if (a !== b) {
        q.push({i: s.push(pop(s) + "skewX(", null, degParen) - 2, x: interpolateNumber(a, b)});
      } else if (b) {
        s.push(pop(s) + "skewX(" + b + degParen);
      }
    }

    function scale(xa, ya, xb, yb, s, q) {
      if (xa !== xb || ya !== yb) {
        var i = s.push(pop(s) + "scale(", null, ",", null, ")");
        q.push({i: i - 4, x: interpolateNumber(xa, xb)}, {i: i - 2, x: interpolateNumber(ya, yb)});
      } else if (xb !== 1 || yb !== 1) {
        s.push(pop(s) + "scale(" + xb + "," + yb + ")");
      }
    }

    return function(a, b) {
      var s = [], // string constants and placeholders
          q = []; // number interpolators
      a = parse(a), b = parse(b);
      translate(a.translateX, a.translateY, b.translateX, b.translateY, s, q);
      rotate(a.rotate, b.rotate, s, q);
      skewX(a.skewX, b.skewX, s, q);
      scale(a.scaleX, a.scaleY, b.scaleX, b.scaleY, s, q);
      a = b = null; // gc
      return function(t) {
        var i = -1, n = q.length, o;
        while (++i < n) s[(o = q[i]).i] = o.x(t);
        return s.join("");
      };
    };
  }

  var interpolateTransformCss = interpolateTransform(parseCss, "px, ", "px)", "deg)");
  var interpolateTransformSvg = interpolateTransform(parseSvg, ", ", ")", ")");

  function cubehelix(hue) {
    return (function cubehelixGamma(y) {
      y = +y;

      function cubehelix(start, end) {
        var h = hue((start = cubehelix$1(start)).h, (end = cubehelix$1(end)).h),
            s = nogamma(start.s, end.s),
            l = nogamma(start.l, end.l),
            opacity = nogamma(start.opacity, end.opacity);
        return function(t) {
          start.h = h(t);
          start.s = s(t);
          start.l = l(Math.pow(t, y));
          start.opacity = opacity(t);
          return start + "";
        };
      }

      cubehelix.gamma = cubehelixGamma;

      return cubehelix;
    })(1);
  }

  cubehelix(hue);
  var cubehelixLong = cubehelix(nogamma);

  function quantize(interpolator, n) {
    var samples = new Array(n);
    for (var i = 0; i < n; ++i) samples[i] = interpolator(i / (n - 1));
    return samples;
  }

  var frame = 0, // is an animation frame pending?
      timeout$1 = 0, // is a timeout pending?
      interval = 0, // are any timers active?
      pokeDelay = 1000, // how frequently we check for clock skew
      taskHead,
      taskTail,
      clockLast = 0,
      clockNow = 0,
      clockSkew = 0,
      clock = typeof performance === "object" && performance.now ? performance : Date,
      setFrame = typeof window === "object" && window.requestAnimationFrame ? window.requestAnimationFrame.bind(window) : function(f) { setTimeout(f, 17); };

  function now() {
    return clockNow || (setFrame(clearNow), clockNow = clock.now() + clockSkew);
  }

  function clearNow() {
    clockNow = 0;
  }

  function Timer() {
    this._call =
    this._time =
    this._next = null;
  }

  Timer.prototype = timer.prototype = {
    constructor: Timer,
    restart: function(callback, delay, time) {
      if (typeof callback !== "function") throw new TypeError("callback is not a function");
      time = (time == null ? now() : +time) + (delay == null ? 0 : +delay);
      if (!this._next && taskTail !== this) {
        if (taskTail) taskTail._next = this;
        else taskHead = this;
        taskTail = this;
      }
      this._call = callback;
      this._time = time;
      sleep();
    },
    stop: function() {
      if (this._call) {
        this._call = null;
        this._time = Infinity;
        sleep();
      }
    }
  };

  function timer(callback, delay, time) {
    var t = new Timer;
    t.restart(callback, delay, time);
    return t;
  }

  function timerFlush() {
    now(); // Get the current time, if not already set.
    ++frame; // Pretend we’ve set an alarm, if we haven’t already.
    var t = taskHead, e;
    while (t) {
      if ((e = clockNow - t._time) >= 0) t._call.call(undefined, e);
      t = t._next;
    }
    --frame;
  }

  function wake() {
    clockNow = (clockLast = clock.now()) + clockSkew;
    frame = timeout$1 = 0;
    try {
      timerFlush();
    } finally {
      frame = 0;
      nap();
      clockNow = 0;
    }
  }

  function poke() {
    var now = clock.now(), delay = now - clockLast;
    if (delay > pokeDelay) clockSkew -= delay, clockLast = now;
  }

  function nap() {
    var t0, t1 = taskHead, t2, time = Infinity;
    while (t1) {
      if (t1._call) {
        if (time > t1._time) time = t1._time;
        t0 = t1, t1 = t1._next;
      } else {
        t2 = t1._next, t1._next = null;
        t1 = t0 ? t0._next = t2 : taskHead = t2;
      }
    }
    taskTail = t0;
    sleep(time);
  }

  function sleep(time) {
    if (frame) return; // Soonest alarm already set, or will be.
    if (timeout$1) timeout$1 = clearTimeout(timeout$1);
    var delay = time - clockNow; // Strictly less than if we recomputed clockNow.
    if (delay > 24) {
      if (time < Infinity) timeout$1 = setTimeout(wake, time - clock.now() - clockSkew);
      if (interval) interval = clearInterval(interval);
    } else {
      if (!interval) clockLast = clock.now(), interval = setInterval(poke, pokeDelay);
      frame = 1, setFrame(wake);
    }
  }

  function timeout(callback, delay, time) {
    var t = new Timer;
    delay = delay == null ? 0 : +delay;
    t.restart(elapsed => {
      t.stop();
      callback(elapsed + delay);
    }, delay, time);
    return t;
  }

  var emptyOn = dispatch("start", "end", "cancel", "interrupt");
  var emptyTween = [];

  var CREATED = 0;
  var SCHEDULED = 1;
  var STARTING = 2;
  var STARTED = 3;
  var RUNNING = 4;
  var ENDING = 5;
  var ENDED = 6;

  function schedule(node, name, id, index, group, timing) {
    var schedules = node.__transition;
    if (!schedules) node.__transition = {};
    else if (id in schedules) return;
    create(node, id, {
      name: name,
      index: index, // For context during callback.
      group: group, // For context during callback.
      on: emptyOn,
      tween: emptyTween,
      time: timing.time,
      delay: timing.delay,
      duration: timing.duration,
      ease: timing.ease,
      timer: null,
      state: CREATED
    });
  }

  function init(node, id) {
    var schedule = get(node, id);
    if (schedule.state > CREATED) throw new Error("too late; already scheduled");
    return schedule;
  }

  function set(node, id) {
    var schedule = get(node, id);
    if (schedule.state > STARTED) throw new Error("too late; already running");
    return schedule;
  }

  function get(node, id) {
    var schedule = node.__transition;
    if (!schedule || !(schedule = schedule[id])) throw new Error("transition not found");
    return schedule;
  }

  function create(node, id, self) {
    var schedules = node.__transition,
        tween;

    // Initialize the self timer when the transition is created.
    // Note the actual delay is not known until the first callback!
    schedules[id] = self;
    self.timer = timer(schedule, 0, self.time);

    function schedule(elapsed) {
      self.state = SCHEDULED;
      self.timer.restart(start, self.delay, self.time);

      // If the elapsed delay is less than our first sleep, start immediately.
      if (self.delay <= elapsed) start(elapsed - self.delay);
    }

    function start(elapsed) {
      var i, j, n, o;

      // If the state is not SCHEDULED, then we previously errored on start.
      if (self.state !== SCHEDULED) return stop();

      for (i in schedules) {
        o = schedules[i];
        if (o.name !== self.name) continue;

        // While this element already has a starting transition during this frame,
        // defer starting an interrupting transition until that transition has a
        // chance to tick (and possibly end); see d3/d3-transition#54!
        if (o.state === STARTED) return timeout(start);

        // Interrupt the active transition, if any.
        if (o.state === RUNNING) {
          o.state = ENDED;
          o.timer.stop();
          o.on.call("interrupt", node, node.__data__, o.index, o.group);
          delete schedules[i];
        }

        // Cancel any pre-empted transitions.
        else if (+i < id) {
          o.state = ENDED;
          o.timer.stop();
          o.on.call("cancel", node, node.__data__, o.index, o.group);
          delete schedules[i];
        }
      }

      // Defer the first tick to end of the current frame; see d3/d3#1576.
      // Note the transition may be canceled after start and before the first tick!
      // Note this must be scheduled before the start event; see d3/d3-transition#16!
      // Assuming this is successful, subsequent callbacks go straight to tick.
      timeout(function() {
        if (self.state === STARTED) {
          self.state = RUNNING;
          self.timer.restart(tick, self.delay, self.time);
          tick(elapsed);
        }
      });

      // Dispatch the start event.
      // Note this must be done before the tween are initialized.
      self.state = STARTING;
      self.on.call("start", node, node.__data__, self.index, self.group);
      if (self.state !== STARTING) return; // interrupted
      self.state = STARTED;

      // Initialize the tween, deleting null tween.
      tween = new Array(n = self.tween.length);
      for (i = 0, j = -1; i < n; ++i) {
        if (o = self.tween[i].value.call(node, node.__data__, self.index, self.group)) {
          tween[++j] = o;
        }
      }
      tween.length = j + 1;
    }

    function tick(elapsed) {
      var t = elapsed < self.duration ? self.ease.call(null, elapsed / self.duration) : (self.timer.restart(stop), self.state = ENDING, 1),
          i = -1,
          n = tween.length;

      while (++i < n) {
        tween[i].call(node, t);
      }

      // Dispatch the end event.
      if (self.state === ENDING) {
        self.on.call("end", node, node.__data__, self.index, self.group);
        stop();
      }
    }

    function stop() {
      self.state = ENDED;
      self.timer.stop();
      delete schedules[id];
      for (var i in schedules) return; // eslint-disable-line no-unused-vars
      delete node.__transition;
    }
  }

  function interrupt(node, name) {
    var schedules = node.__transition,
        schedule,
        active,
        empty = true,
        i;

    if (!schedules) return;

    name = name == null ? null : name + "";

    for (i in schedules) {
      if ((schedule = schedules[i]).name !== name) { empty = false; continue; }
      active = schedule.state > STARTING && schedule.state < ENDING;
      schedule.state = ENDED;
      schedule.timer.stop();
      schedule.on.call(active ? "interrupt" : "cancel", node, node.__data__, schedule.index, schedule.group);
      delete schedules[i];
    }

    if (empty) delete node.__transition;
  }

  function selection_interrupt(name) {
    return this.each(function() {
      interrupt(this, name);
    });
  }

  function tweenRemove(id, name) {
    var tween0, tween1;
    return function() {
      var schedule = set(this, id),
          tween = schedule.tween;

      // If this node shared tween with the previous node,
      // just assign the updated shared tween and we’re done!
      // Otherwise, copy-on-write.
      if (tween !== tween0) {
        tween1 = tween0 = tween;
        for (var i = 0, n = tween1.length; i < n; ++i) {
          if (tween1[i].name === name) {
            tween1 = tween1.slice();
            tween1.splice(i, 1);
            break;
          }
        }
      }

      schedule.tween = tween1;
    };
  }

  function tweenFunction(id, name, value) {
    var tween0, tween1;
    if (typeof value !== "function") throw new Error;
    return function() {
      var schedule = set(this, id),
          tween = schedule.tween;

      // If this node shared tween with the previous node,
      // just assign the updated shared tween and we’re done!
      // Otherwise, copy-on-write.
      if (tween !== tween0) {
        tween1 = (tween0 = tween).slice();
        for (var t = {name: name, value: value}, i = 0, n = tween1.length; i < n; ++i) {
          if (tween1[i].name === name) {
            tween1[i] = t;
            break;
          }
        }
        if (i === n) tween1.push(t);
      }

      schedule.tween = tween1;
    };
  }

  function transition_tween(name, value) {
    var id = this._id;

    name += "";

    if (arguments.length < 2) {
      var tween = get(this.node(), id).tween;
      for (var i = 0, n = tween.length, t; i < n; ++i) {
        if ((t = tween[i]).name === name) {
          return t.value;
        }
      }
      return null;
    }

    return this.each((value == null ? tweenRemove : tweenFunction)(id, name, value));
  }

  function tweenValue(transition, name, value) {
    var id = transition._id;

    transition.each(function() {
      var schedule = set(this, id);
      (schedule.value || (schedule.value = {}))[name] = value.apply(this, arguments);
    });

    return function(node) {
      return get(node, id).value[name];
    };
  }

  function interpolate(a, b) {
    var c;
    return (typeof b === "number" ? interpolateNumber
        : b instanceof color ? interpolateRgb
        : (c = color(b)) ? (b = c, interpolateRgb)
        : interpolateString)(a, b);
  }

  function attrRemove(name) {
    return function() {
      this.removeAttribute(name);
    };
  }

  function attrRemoveNS(fullname) {
    return function() {
      this.removeAttributeNS(fullname.space, fullname.local);
    };
  }

  function attrConstant(name, interpolate, value1) {
    var string00,
        string1 = value1 + "",
        interpolate0;
    return function() {
      var string0 = this.getAttribute(name);
      return string0 === string1 ? null
          : string0 === string00 ? interpolate0
          : interpolate0 = interpolate(string00 = string0, value1);
    };
  }

  function attrConstantNS(fullname, interpolate, value1) {
    var string00,
        string1 = value1 + "",
        interpolate0;
    return function() {
      var string0 = this.getAttributeNS(fullname.space, fullname.local);
      return string0 === string1 ? null
          : string0 === string00 ? interpolate0
          : interpolate0 = interpolate(string00 = string0, value1);
    };
  }

  function attrFunction(name, interpolate, value) {
    var string00,
        string10,
        interpolate0;
    return function() {
      var string0, value1 = value(this), string1;
      if (value1 == null) return void this.removeAttribute(name);
      string0 = this.getAttribute(name);
      string1 = value1 + "";
      return string0 === string1 ? null
          : string0 === string00 && string1 === string10 ? interpolate0
          : (string10 = string1, interpolate0 = interpolate(string00 = string0, value1));
    };
  }

  function attrFunctionNS(fullname, interpolate, value) {
    var string00,
        string10,
        interpolate0;
    return function() {
      var string0, value1 = value(this), string1;
      if (value1 == null) return void this.removeAttributeNS(fullname.space, fullname.local);
      string0 = this.getAttributeNS(fullname.space, fullname.local);
      string1 = value1 + "";
      return string0 === string1 ? null
          : string0 === string00 && string1 === string10 ? interpolate0
          : (string10 = string1, interpolate0 = interpolate(string00 = string0, value1));
    };
  }

  function transition_attr(name, value) {
    var fullname = namespace(name), i = fullname === "transform" ? interpolateTransformSvg : interpolate;
    return this.attrTween(name, typeof value === "function"
        ? (fullname.local ? attrFunctionNS : attrFunction)(fullname, i, tweenValue(this, "attr." + name, value))
        : value == null ? (fullname.local ? attrRemoveNS : attrRemove)(fullname)
        : (fullname.local ? attrConstantNS : attrConstant)(fullname, i, value));
  }

  function attrInterpolate(name, i) {
    return function(t) {
      this.setAttribute(name, i.call(this, t));
    };
  }

  function attrInterpolateNS(fullname, i) {
    return function(t) {
      this.setAttributeNS(fullname.space, fullname.local, i.call(this, t));
    };
  }

  function attrTweenNS(fullname, value) {
    var t0, i0;
    function tween() {
      var i = value.apply(this, arguments);
      if (i !== i0) t0 = (i0 = i) && attrInterpolateNS(fullname, i);
      return t0;
    }
    tween._value = value;
    return tween;
  }

  function attrTween(name, value) {
    var t0, i0;
    function tween() {
      var i = value.apply(this, arguments);
      if (i !== i0) t0 = (i0 = i) && attrInterpolate(name, i);
      return t0;
    }
    tween._value = value;
    return tween;
  }

  function transition_attrTween(name, value) {
    var key = "attr." + name;
    if (arguments.length < 2) return (key = this.tween(key)) && key._value;
    if (value == null) return this.tween(key, null);
    if (typeof value !== "function") throw new Error;
    var fullname = namespace(name);
    return this.tween(key, (fullname.local ? attrTweenNS : attrTween)(fullname, value));
  }

  function delayFunction(id, value) {
    return function() {
      init(this, id).delay = +value.apply(this, arguments);
    };
  }

  function delayConstant(id, value) {
    return value = +value, function() {
      init(this, id).delay = value;
    };
  }

  function transition_delay(value) {
    var id = this._id;

    return arguments.length
        ? this.each((typeof value === "function"
            ? delayFunction
            : delayConstant)(id, value))
        : get(this.node(), id).delay;
  }

  function durationFunction(id, value) {
    return function() {
      set(this, id).duration = +value.apply(this, arguments);
    };
  }

  function durationConstant(id, value) {
    return value = +value, function() {
      set(this, id).duration = value;
    };
  }

  function transition_duration(value) {
    var id = this._id;

    return arguments.length
        ? this.each((typeof value === "function"
            ? durationFunction
            : durationConstant)(id, value))
        : get(this.node(), id).duration;
  }

  function easeConstant(id, value) {
    if (typeof value !== "function") throw new Error;
    return function() {
      set(this, id).ease = value;
    };
  }

  function transition_ease(value) {
    var id = this._id;

    return arguments.length
        ? this.each(easeConstant(id, value))
        : get(this.node(), id).ease;
  }

  function easeVarying(id, value) {
    return function() {
      var v = value.apply(this, arguments);
      if (typeof v !== "function") throw new Error;
      set(this, id).ease = v;
    };
  }

  function transition_easeVarying(value) {
    if (typeof value !== "function") throw new Error;
    return this.each(easeVarying(this._id, value));
  }

  function transition_filter(match) {
    if (typeof match !== "function") match = matcher(match);

    for (var groups = this._groups, m = groups.length, subgroups = new Array(m), j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, subgroup = subgroups[j] = [], node, i = 0; i < n; ++i) {
        if ((node = group[i]) && match.call(node, node.__data__, i, group)) {
          subgroup.push(node);
        }
      }
    }

    return new Transition(subgroups, this._parents, this._name, this._id);
  }

  function transition_merge(transition) {
    if (transition._id !== this._id) throw new Error;

    for (var groups0 = this._groups, groups1 = transition._groups, m0 = groups0.length, m1 = groups1.length, m = Math.min(m0, m1), merges = new Array(m0), j = 0; j < m; ++j) {
      for (var group0 = groups0[j], group1 = groups1[j], n = group0.length, merge = merges[j] = new Array(n), node, i = 0; i < n; ++i) {
        if (node = group0[i] || group1[i]) {
          merge[i] = node;
        }
      }
    }

    for (; j < m0; ++j) {
      merges[j] = groups0[j];
    }

    return new Transition(merges, this._parents, this._name, this._id);
  }

  function start(name) {
    return (name + "").trim().split(/^|\s+/).every(function(t) {
      var i = t.indexOf(".");
      if (i >= 0) t = t.slice(0, i);
      return !t || t === "start";
    });
  }

  function onFunction(id, name, listener) {
    var on0, on1, sit = start(name) ? init : set;
    return function() {
      var schedule = sit(this, id),
          on = schedule.on;

      // If this node shared a dispatch with the previous node,
      // just assign the updated shared dispatch and we’re done!
      // Otherwise, copy-on-write.
      if (on !== on0) (on1 = (on0 = on).copy()).on(name, listener);

      schedule.on = on1;
    };
  }

  function transition_on(name, listener) {
    var id = this._id;

    return arguments.length < 2
        ? get(this.node(), id).on.on(name)
        : this.each(onFunction(id, name, listener));
  }

  function removeFunction(id) {
    return function() {
      var parent = this.parentNode;
      for (var i in this.__transition) if (+i !== id) return;
      if (parent) parent.removeChild(this);
    };
  }

  function transition_remove() {
    return this.on("end.remove", removeFunction(this._id));
  }

  function transition_select(select) {
    var name = this._name,
        id = this._id;

    if (typeof select !== "function") select = selector(select);

    for (var groups = this._groups, m = groups.length, subgroups = new Array(m), j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, subgroup = subgroups[j] = new Array(n), node, subnode, i = 0; i < n; ++i) {
        if ((node = group[i]) && (subnode = select.call(node, node.__data__, i, group))) {
          if ("__data__" in node) subnode.__data__ = node.__data__;
          subgroup[i] = subnode;
          schedule(subgroup[i], name, id, i, subgroup, get(node, id));
        }
      }
    }

    return new Transition(subgroups, this._parents, name, id);
  }

  function transition_selectAll(select) {
    var name = this._name,
        id = this._id;

    if (typeof select !== "function") select = selectorAll(select);

    for (var groups = this._groups, m = groups.length, subgroups = [], parents = [], j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, node, i = 0; i < n; ++i) {
        if (node = group[i]) {
          for (var children = select.call(node, node.__data__, i, group), child, inherit = get(node, id), k = 0, l = children.length; k < l; ++k) {
            if (child = children[k]) {
              schedule(child, name, id, k, children, inherit);
            }
          }
          subgroups.push(children);
          parents.push(node);
        }
      }
    }

    return new Transition(subgroups, parents, name, id);
  }

  var Selection = selection.prototype.constructor;

  function transition_selection() {
    return new Selection(this._groups, this._parents);
  }

  function styleNull(name, interpolate) {
    var string00,
        string10,
        interpolate0;
    return function() {
      var string0 = styleValue(this, name),
          string1 = (this.style.removeProperty(name), styleValue(this, name));
      return string0 === string1 ? null
          : string0 === string00 && string1 === string10 ? interpolate0
          : interpolate0 = interpolate(string00 = string0, string10 = string1);
    };
  }

  function styleRemove(name) {
    return function() {
      this.style.removeProperty(name);
    };
  }

  function styleConstant(name, interpolate, value1) {
    var string00,
        string1 = value1 + "",
        interpolate0;
    return function() {
      var string0 = styleValue(this, name);
      return string0 === string1 ? null
          : string0 === string00 ? interpolate0
          : interpolate0 = interpolate(string00 = string0, value1);
    };
  }

  function styleFunction(name, interpolate, value) {
    var string00,
        string10,
        interpolate0;
    return function() {
      var string0 = styleValue(this, name),
          value1 = value(this),
          string1 = value1 + "";
      if (value1 == null) string1 = value1 = (this.style.removeProperty(name), styleValue(this, name));
      return string0 === string1 ? null
          : string0 === string00 && string1 === string10 ? interpolate0
          : (string10 = string1, interpolate0 = interpolate(string00 = string0, value1));
    };
  }

  function styleMaybeRemove(id, name) {
    var on0, on1, listener0, key = "style." + name, event = "end." + key, remove;
    return function() {
      var schedule = set(this, id),
          on = schedule.on,
          listener = schedule.value[key] == null ? remove || (remove = styleRemove(name)) : undefined;

      // If this node shared a dispatch with the previous node,
      // just assign the updated shared dispatch and we’re done!
      // Otherwise, copy-on-write.
      if (on !== on0 || listener0 !== listener) (on1 = (on0 = on).copy()).on(event, listener0 = listener);

      schedule.on = on1;
    };
  }

  function transition_style(name, value, priority) {
    var i = (name += "") === "transform" ? interpolateTransformCss : interpolate;
    return value == null ? this
        .styleTween(name, styleNull(name, i))
        .on("end.style." + name, styleRemove(name))
      : typeof value === "function" ? this
        .styleTween(name, styleFunction(name, i, tweenValue(this, "style." + name, value)))
        .each(styleMaybeRemove(this._id, name))
      : this
        .styleTween(name, styleConstant(name, i, value), priority)
        .on("end.style." + name, null);
  }

  function styleInterpolate(name, i, priority) {
    return function(t) {
      this.style.setProperty(name, i.call(this, t), priority);
    };
  }

  function styleTween(name, value, priority) {
    var t, i0;
    function tween() {
      var i = value.apply(this, arguments);
      if (i !== i0) t = (i0 = i) && styleInterpolate(name, i, priority);
      return t;
    }
    tween._value = value;
    return tween;
  }

  function transition_styleTween(name, value, priority) {
    var key = "style." + (name += "");
    if (arguments.length < 2) return (key = this.tween(key)) && key._value;
    if (value == null) return this.tween(key, null);
    if (typeof value !== "function") throw new Error;
    return this.tween(key, styleTween(name, value, priority == null ? "" : priority));
  }

  function textConstant(value) {
    return function() {
      this.textContent = value;
    };
  }

  function textFunction(value) {
    return function() {
      var value1 = value(this);
      this.textContent = value1 == null ? "" : value1;
    };
  }

  function transition_text(value) {
    return this.tween("text", typeof value === "function"
        ? textFunction(tweenValue(this, "text", value))
        : textConstant(value == null ? "" : value + ""));
  }

  function textInterpolate(i) {
    return function(t) {
      this.textContent = i.call(this, t);
    };
  }

  function textTween(value) {
    var t0, i0;
    function tween() {
      var i = value.apply(this, arguments);
      if (i !== i0) t0 = (i0 = i) && textInterpolate(i);
      return t0;
    }
    tween._value = value;
    return tween;
  }

  function transition_textTween(value) {
    var key = "text";
    if (arguments.length < 1) return (key = this.tween(key)) && key._value;
    if (value == null) return this.tween(key, null);
    if (typeof value !== "function") throw new Error;
    return this.tween(key, textTween(value));
  }

  function transition_transition() {
    var name = this._name,
        id0 = this._id,
        id1 = newId();

    for (var groups = this._groups, m = groups.length, j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, node, i = 0; i < n; ++i) {
        if (node = group[i]) {
          var inherit = get(node, id0);
          schedule(node, name, id1, i, group, {
            time: inherit.time + inherit.delay + inherit.duration,
            delay: 0,
            duration: inherit.duration,
            ease: inherit.ease
          });
        }
      }
    }

    return new Transition(groups, this._parents, name, id1);
  }

  function transition_end() {
    var on0, on1, that = this, id = that._id, size = that.size();
    return new Promise(function(resolve, reject) {
      var cancel = {value: reject},
          end = {value: function() { if (--size === 0) resolve(); }};

      that.each(function() {
        var schedule = set(this, id),
            on = schedule.on;

        // If this node shared a dispatch with the previous node,
        // just assign the updated shared dispatch and we’re done!
        // Otherwise, copy-on-write.
        if (on !== on0) {
          on1 = (on0 = on).copy();
          on1._.cancel.push(cancel);
          on1._.interrupt.push(cancel);
          on1._.end.push(end);
        }

        schedule.on = on1;
      });

      // The selection was empty, resolve end immediately
      if (size === 0) resolve();
    });
  }

  var id = 0;

  function Transition(groups, parents, name, id) {
    this._groups = groups;
    this._parents = parents;
    this._name = name;
    this._id = id;
  }

  function newId() {
    return ++id;
  }

  var selection_prototype = selection.prototype;

  Transition.prototype = {
    constructor: Transition,
    select: transition_select,
    selectAll: transition_selectAll,
    selectChild: selection_prototype.selectChild,
    selectChildren: selection_prototype.selectChildren,
    filter: transition_filter,
    merge: transition_merge,
    selection: transition_selection,
    transition: transition_transition,
    call: selection_prototype.call,
    nodes: selection_prototype.nodes,
    node: selection_prototype.node,
    size: selection_prototype.size,
    empty: selection_prototype.empty,
    each: selection_prototype.each,
    on: transition_on,
    attr: transition_attr,
    attrTween: transition_attrTween,
    style: transition_style,
    styleTween: transition_styleTween,
    text: transition_text,
    textTween: transition_textTween,
    remove: transition_remove,
    tween: transition_tween,
    delay: transition_delay,
    duration: transition_duration,
    ease: transition_ease,
    easeVarying: transition_easeVarying,
    end: transition_end,
    [Symbol.iterator]: selection_prototype[Symbol.iterator]
  };

  function cubicInOut(t) {
    return ((t *= 2) <= 1 ? t * t * t : (t -= 2) * t * t + 2) / 2;
  }

  var defaultTiming = {
    time: null, // Set on use.
    delay: 0,
    duration: 250,
    ease: cubicInOut
  };

  function inherit(node, id) {
    var timing;
    while (!(timing = node.__transition) || !(timing = timing[id])) {
      if (!(node = node.parentNode)) {
        throw new Error(`transition ${id} not found`);
      }
    }
    return timing;
  }

  function selection_transition(name) {
    var id,
        timing;

    if (name instanceof Transition) {
      id = name._id, name = name._name;
    } else {
      id = newId(), (timing = defaultTiming).time = now(), name = name == null ? null : name + "";
    }

    for (var groups = this._groups, m = groups.length, j = 0; j < m; ++j) {
      for (var group = groups[j], n = group.length, node, i = 0; i < n; ++i) {
        if (node = group[i]) {
          schedule(node, name, id, i, group, timing || inherit(node, id));
        }
      }
    }

    return new Transition(groups, this._parents, name, id);
  }

  selection.prototype.interrupt = selection_interrupt;
  selection.prototype.transition = selection_transition;

  var abs$1 = Math.abs;
  var cos$1 = Math.cos;
  var sin$1 = Math.sin;
  var pi$2 = Math.PI;
  var halfPi$1 = pi$2 / 2;
  var tau$2 = pi$2 * 2;
  var max$1 = Math.max;
  var epsilon$2 = 1e-12;

  function range(i, j) {
    return Array.from({length: j - i}, (_, k) => i + k);
  }

  function compareValue(compare) {
    return function(a, b) {
      return compare(
        a.source.value + a.target.value,
        b.source.value + b.target.value
      );
    };
  }

  function chordDirected() {
    return chord(true, false);
  }

  function chord(directed, transpose) {
    var padAngle = 0,
        sortGroups = null,
        sortSubgroups = null,
        sortChords = null;

    function chord(matrix) {
      var n = matrix.length,
          groupSums = new Array(n),
          groupIndex = range(0, n),
          chords = new Array(n * n),
          groups = new Array(n),
          k = 0, dx;

      matrix = Float64Array.from({length: n * n}, transpose
          ? (_, i) => matrix[i % n][i / n | 0]
          : (_, i) => matrix[i / n | 0][i % n]);

      // Compute the scaling factor from value to angle in [0, 2pi].
      for (let i = 0; i < n; ++i) {
        let x = 0;
        for (let j = 0; j < n; ++j) x += matrix[i * n + j] + directed * matrix[j * n + i];
        k += groupSums[i] = x;
      }
      k = max$1(0, tau$2 - padAngle * n) / k;
      dx = k ? padAngle : tau$2 / n;

      // Compute the angles for each group and constituent chord.
      {
        let x = 0;
        if (sortGroups) groupIndex.sort((a, b) => sortGroups(groupSums[a], groupSums[b]));
        for (const i of groupIndex) {
          const x0 = x;
          if (directed) {
            const subgroupIndex = range(~n + 1, n).filter(j => j < 0 ? matrix[~j * n + i] : matrix[i * n + j]);
            if (sortSubgroups) subgroupIndex.sort((a, b) => sortSubgroups(a < 0 ? -matrix[~a * n + i] : matrix[i * n + a], b < 0 ? -matrix[~b * n + i] : matrix[i * n + b]));
            for (const j of subgroupIndex) {
              if (j < 0) {
                const chord = chords[~j * n + i] || (chords[~j * n + i] = {source: null, target: null});
                chord.target = {index: i, startAngle: x, endAngle: x += matrix[~j * n + i] * k, value: matrix[~j * n + i]};
              } else {
                const chord = chords[i * n + j] || (chords[i * n + j] = {source: null, target: null});
                chord.source = {index: i, startAngle: x, endAngle: x += matrix[i * n + j] * k, value: matrix[i * n + j]};
              }
            }
            groups[i] = {index: i, startAngle: x0, endAngle: x, value: groupSums[i]};
          } else {
            const subgroupIndex = range(0, n).filter(j => matrix[i * n + j] || matrix[j * n + i]);
            if (sortSubgroups) subgroupIndex.sort((a, b) => sortSubgroups(matrix[i * n + a], matrix[i * n + b]));
            for (const j of subgroupIndex) {
              let chord;
              if (i < j) {
                chord = chords[i * n + j] || (chords[i * n + j] = {source: null, target: null});
                chord.source = {index: i, startAngle: x, endAngle: x += matrix[i * n + j] * k, value: matrix[i * n + j]};
              } else {
                chord = chords[j * n + i] || (chords[j * n + i] = {source: null, target: null});
                chord.target = {index: i, startAngle: x, endAngle: x += matrix[i * n + j] * k, value: matrix[i * n + j]};
                if (i === j) chord.source = chord.target;
              }
              if (chord.source && chord.target && chord.source.value < chord.target.value) {
                const source = chord.source;
                chord.source = chord.target;
                chord.target = source;
              }
            }
            groups[i] = {index: i, startAngle: x0, endAngle: x, value: groupSums[i]};
          }
          x += dx;
        }
      }

      // Remove empty chords.
      chords = Object.values(chords);
      chords.groups = groups;
      return sortChords ? chords.sort(sortChords) : chords;
    }

    chord.padAngle = function(_) {
      return arguments.length ? (padAngle = max$1(0, _), chord) : padAngle;
    };

    chord.sortGroups = function(_) {
      return arguments.length ? (sortGroups = _, chord) : sortGroups;
    };

    chord.sortSubgroups = function(_) {
      return arguments.length ? (sortSubgroups = _, chord) : sortSubgroups;
    };

    chord.sortChords = function(_) {
      return arguments.length ? (_ == null ? sortChords = null : (sortChords = compareValue(_))._ = _, chord) : sortChords && sortChords._;
    };

    return chord;
  }

  const pi$1 = Math.PI,
      tau$1 = 2 * pi$1,
      epsilon$1 = 1e-6,
      tauEpsilon = tau$1 - epsilon$1;

  function Path() {
    this._x0 = this._y0 = // start of current subpath
    this._x1 = this._y1 = null; // end of current subpath
    this._ = "";
  }

  function path() {
    return new Path;
  }

  Path.prototype = path.prototype = {
    constructor: Path,
    moveTo: function(x, y) {
      this._ += "M" + (this._x0 = this._x1 = +x) + "," + (this._y0 = this._y1 = +y);
    },
    closePath: function() {
      if (this._x1 !== null) {
        this._x1 = this._x0, this._y1 = this._y0;
        this._ += "Z";
      }
    },
    lineTo: function(x, y) {
      this._ += "L" + (this._x1 = +x) + "," + (this._y1 = +y);
    },
    quadraticCurveTo: function(x1, y1, x, y) {
      this._ += "Q" + (+x1) + "," + (+y1) + "," + (this._x1 = +x) + "," + (this._y1 = +y);
    },
    bezierCurveTo: function(x1, y1, x2, y2, x, y) {
      this._ += "C" + (+x1) + "," + (+y1) + "," + (+x2) + "," + (+y2) + "," + (this._x1 = +x) + "," + (this._y1 = +y);
    },
    arcTo: function(x1, y1, x2, y2, r) {
      x1 = +x1, y1 = +y1, x2 = +x2, y2 = +y2, r = +r;
      var x0 = this._x1,
          y0 = this._y1,
          x21 = x2 - x1,
          y21 = y2 - y1,
          x01 = x0 - x1,
          y01 = y0 - y1,
          l01_2 = x01 * x01 + y01 * y01;

      // Is the radius negative? Error.
      if (r < 0) throw new Error("negative radius: " + r);

      // Is this path empty? Move to (x1,y1).
      if (this._x1 === null) {
        this._ += "M" + (this._x1 = x1) + "," + (this._y1 = y1);
      }

      // Or, is (x1,y1) coincident with (x0,y0)? Do nothing.
      else if (!(l01_2 > epsilon$1));

      // Or, are (x0,y0), (x1,y1) and (x2,y2) collinear?
      // Equivalently, is (x1,y1) coincident with (x2,y2)?
      // Or, is the radius zero? Line to (x1,y1).
      else if (!(Math.abs(y01 * x21 - y21 * x01) > epsilon$1) || !r) {
        this._ += "L" + (this._x1 = x1) + "," + (this._y1 = y1);
      }

      // Otherwise, draw an arc!
      else {
        var x20 = x2 - x0,
            y20 = y2 - y0,
            l21_2 = x21 * x21 + y21 * y21,
            l20_2 = x20 * x20 + y20 * y20,
            l21 = Math.sqrt(l21_2),
            l01 = Math.sqrt(l01_2),
            l = r * Math.tan((pi$1 - Math.acos((l21_2 + l01_2 - l20_2) / (2 * l21 * l01))) / 2),
            t01 = l / l01,
            t21 = l / l21;

        // If the start tangent is not coincident with (x0,y0), line to.
        if (Math.abs(t01 - 1) > epsilon$1) {
          this._ += "L" + (x1 + t01 * x01) + "," + (y1 + t01 * y01);
        }

        this._ += "A" + r + "," + r + ",0,0," + (+(y01 * x20 > x01 * y20)) + "," + (this._x1 = x1 + t21 * x21) + "," + (this._y1 = y1 + t21 * y21);
      }
    },
    arc: function(x, y, r, a0, a1, ccw) {
      x = +x, y = +y, r = +r, ccw = !!ccw;
      var dx = r * Math.cos(a0),
          dy = r * Math.sin(a0),
          x0 = x + dx,
          y0 = y + dy,
          cw = 1 ^ ccw,
          da = ccw ? a0 - a1 : a1 - a0;

      // Is the radius negative? Error.
      if (r < 0) throw new Error("negative radius: " + r);

      // Is this path empty? Move to (x0,y0).
      if (this._x1 === null) {
        this._ += "M" + x0 + "," + y0;
      }

      // Or, is (x0,y0) not coincident with the previous point? Line to (x0,y0).
      else if (Math.abs(this._x1 - x0) > epsilon$1 || Math.abs(this._y1 - y0) > epsilon$1) {
        this._ += "L" + x0 + "," + y0;
      }

      // Is this arc empty? We’re done.
      if (!r) return;

      // Does the angle go the wrong way? Flip the direction.
      if (da < 0) da = da % tau$1 + tau$1;

      // Is this a complete circle? Draw two arcs to complete the circle.
      if (da > tauEpsilon) {
        this._ += "A" + r + "," + r + ",0,1," + cw + "," + (x - dx) + "," + (y - dy) + "A" + r + "," + r + ",0,1," + cw + "," + (this._x1 = x0) + "," + (this._y1 = y0);
      }

      // Is this arc non-empty? Draw an arc!
      else if (da > epsilon$1) {
        this._ += "A" + r + "," + r + ",0," + (+(da >= pi$1)) + "," + cw + "," + (this._x1 = x + r * Math.cos(a1)) + "," + (this._y1 = y + r * Math.sin(a1));
      }
    },
    rect: function(x, y, w, h) {
      this._ += "M" + (this._x0 = this._x1 = +x) + "," + (this._y0 = this._y1 = +y) + "h" + (+w) + "v" + (+h) + "h" + (-w) + "Z";
    },
    toString: function() {
      return this._;
    }
  };

  var slice = Array.prototype.slice;

  function constant$1(x) {
    return function() {
      return x;
    };
  }

  function defaultSource(d) {
    return d.source;
  }

  function defaultTarget(d) {
    return d.target;
  }

  function defaultRadius(d) {
    return d.radius;
  }

  function defaultStartAngle(d) {
    return d.startAngle;
  }

  function defaultEndAngle(d) {
    return d.endAngle;
  }

  function defaultPadAngle() {
    return 0;
  }

  function defaultArrowheadRadius() {
    return 10;
  }

  function ribbon(headRadius) {
    var source = defaultSource,
        target = defaultTarget,
        sourceRadius = defaultRadius,
        targetRadius = defaultRadius,
        startAngle = defaultStartAngle,
        endAngle = defaultEndAngle,
        padAngle = defaultPadAngle,
        context = null;

    function ribbon() {
      var buffer,
          s = source.apply(this, arguments),
          t = target.apply(this, arguments),
          ap = padAngle.apply(this, arguments) / 2,
          argv = slice.call(arguments),
          sr = +sourceRadius.apply(this, (argv[0] = s, argv)),
          sa0 = startAngle.apply(this, argv) - halfPi$1,
          sa1 = endAngle.apply(this, argv) - halfPi$1,
          tr = +targetRadius.apply(this, (argv[0] = t, argv)),
          ta0 = startAngle.apply(this, argv) - halfPi$1,
          ta1 = endAngle.apply(this, argv) - halfPi$1;

      if (!context) context = buffer = path();

      if (ap > epsilon$2) {
        if (abs$1(sa1 - sa0) > ap * 2 + epsilon$2) sa1 > sa0 ? (sa0 += ap, sa1 -= ap) : (sa0 -= ap, sa1 += ap);
        else sa0 = sa1 = (sa0 + sa1) / 2;
        if (abs$1(ta1 - ta0) > ap * 2 + epsilon$2) ta1 > ta0 ? (ta0 += ap, ta1 -= ap) : (ta0 -= ap, ta1 += ap);
        else ta0 = ta1 = (ta0 + ta1) / 2;
      }

      context.moveTo(sr * cos$1(sa0), sr * sin$1(sa0));
      context.arc(0, 0, sr, sa0, sa1);
      if (sa0 !== ta0 || sa1 !== ta1) {
        if (headRadius) {
          var hr = +headRadius.apply(this, arguments), tr2 = tr - hr, ta2 = (ta0 + ta1) / 2;
          context.quadraticCurveTo(0, 0, tr2 * cos$1(ta0), tr2 * sin$1(ta0));
          context.lineTo(tr * cos$1(ta2), tr * sin$1(ta2));
          context.lineTo(tr2 * cos$1(ta1), tr2 * sin$1(ta1));
        } else {
          context.quadraticCurveTo(0, 0, tr * cos$1(ta0), tr * sin$1(ta0));
          context.arc(0, 0, tr, ta0, ta1);
        }
      }
      context.quadraticCurveTo(0, 0, sr * cos$1(sa0), sr * sin$1(sa0));
      context.closePath();

      if (buffer) return context = null, buffer + "" || null;
    }

    if (headRadius) ribbon.headRadius = function(_) {
      return arguments.length ? (headRadius = typeof _ === "function" ? _ : constant$1(+_), ribbon) : headRadius;
    };

    ribbon.radius = function(_) {
      return arguments.length ? (sourceRadius = targetRadius = typeof _ === "function" ? _ : constant$1(+_), ribbon) : sourceRadius;
    };

    ribbon.sourceRadius = function(_) {
      return arguments.length ? (sourceRadius = typeof _ === "function" ? _ : constant$1(+_), ribbon) : sourceRadius;
    };

    ribbon.targetRadius = function(_) {
      return arguments.length ? (targetRadius = typeof _ === "function" ? _ : constant$1(+_), ribbon) : targetRadius;
    };

    ribbon.startAngle = function(_) {
      return arguments.length ? (startAngle = typeof _ === "function" ? _ : constant$1(+_), ribbon) : startAngle;
    };

    ribbon.endAngle = function(_) {
      return arguments.length ? (endAngle = typeof _ === "function" ? _ : constant$1(+_), ribbon) : endAngle;
    };

    ribbon.padAngle = function(_) {
      return arguments.length ? (padAngle = typeof _ === "function" ? _ : constant$1(+_), ribbon) : padAngle;
    };

    ribbon.source = function(_) {
      return arguments.length ? (source = _, ribbon) : source;
    };

    ribbon.target = function(_) {
      return arguments.length ? (target = _, ribbon) : target;
    };

    ribbon.context = function(_) {
      return arguments.length ? ((context = _ == null ? null : _), ribbon) : context;
    };

    return ribbon;
  }

  function ribbonArrow() {
    return ribbon(defaultArrowheadRadius);
  }

  function formatDecimal(x) {
    return Math.abs(x = Math.round(x)) >= 1e21
        ? x.toLocaleString("en").replace(/,/g, "")
        : x.toString(10);
  }

  // Computes the decimal coefficient and exponent of the specified number x with
  // significant digits p, where x is positive and p is in [1, 21] or undefined.
  // For example, formatDecimalParts(1.23) returns ["123", 0].
  function formatDecimalParts(x, p) {
    if ((i = (x = p ? x.toExponential(p - 1) : x.toExponential()).indexOf("e")) < 0) return null; // NaN, ±Infinity
    var i, coefficient = x.slice(0, i);

    // The string returned by toExponential either has the form \d\.\d+e[-+]\d+
    // (e.g., 1.2e+3) or the form \de[-+]\d+ (e.g., 1e+3).
    return [
      coefficient.length > 1 ? coefficient[0] + coefficient.slice(2) : coefficient,
      +x.slice(i + 1)
    ];
  }

  function exponent(x) {
    return x = formatDecimalParts(Math.abs(x)), x ? x[1] : NaN;
  }

  function formatGroup(grouping, thousands) {
    return function(value, width) {
      var i = value.length,
          t = [],
          j = 0,
          g = grouping[0],
          length = 0;

      while (i > 0 && g > 0) {
        if (length + g + 1 > width) g = Math.max(1, width - length);
        t.push(value.substring(i -= g, i + g));
        if ((length += g + 1) > width) break;
        g = grouping[j = (j + 1) % grouping.length];
      }

      return t.reverse().join(thousands);
    };
  }

  function formatNumerals(numerals) {
    return function(value) {
      return value.replace(/[0-9]/g, function(i) {
        return numerals[+i];
      });
    };
  }

  // [[fill]align][sign][symbol][0][width][,][.precision][~][type]
  var re = /^(?:(.)?([<>=^]))?([+\-( ])?([$#])?(0)?(\d+)?(,)?(\.\d+)?(~)?([a-z%])?$/i;

  function formatSpecifier(specifier) {
    if (!(match = re.exec(specifier))) throw new Error("invalid format: " + specifier);
    var match;
    return new FormatSpecifier({
      fill: match[1],
      align: match[2],
      sign: match[3],
      symbol: match[4],
      zero: match[5],
      width: match[6],
      comma: match[7],
      precision: match[8] && match[8].slice(1),
      trim: match[9],
      type: match[10]
    });
  }

  formatSpecifier.prototype = FormatSpecifier.prototype; // instanceof

  function FormatSpecifier(specifier) {
    this.fill = specifier.fill === undefined ? " " : specifier.fill + "";
    this.align = specifier.align === undefined ? ">" : specifier.align + "";
    this.sign = specifier.sign === undefined ? "-" : specifier.sign + "";
    this.symbol = specifier.symbol === undefined ? "" : specifier.symbol + "";
    this.zero = !!specifier.zero;
    this.width = specifier.width === undefined ? undefined : +specifier.width;
    this.comma = !!specifier.comma;
    this.precision = specifier.precision === undefined ? undefined : +specifier.precision;
    this.trim = !!specifier.trim;
    this.type = specifier.type === undefined ? "" : specifier.type + "";
  }

  FormatSpecifier.prototype.toString = function() {
    return this.fill
        + this.align
        + this.sign
        + this.symbol
        + (this.zero ? "0" : "")
        + (this.width === undefined ? "" : Math.max(1, this.width | 0))
        + (this.comma ? "," : "")
        + (this.precision === undefined ? "" : "." + Math.max(0, this.precision | 0))
        + (this.trim ? "~" : "")
        + this.type;
  };

  // Trims insignificant zeros, e.g., replaces 1.2000k with 1.2k.
  function formatTrim(s) {
    out: for (var n = s.length, i = 1, i0 = -1, i1; i < n; ++i) {
      switch (s[i]) {
        case ".": i0 = i1 = i; break;
        case "0": if (i0 === 0) i0 = i; i1 = i; break;
        default: if (!+s[i]) break out; if (i0 > 0) i0 = 0; break;
      }
    }
    return i0 > 0 ? s.slice(0, i0) + s.slice(i1 + 1) : s;
  }

  var prefixExponent;

  function formatPrefixAuto(x, p) {
    var d = formatDecimalParts(x, p);
    if (!d) return x + "";
    var coefficient = d[0],
        exponent = d[1],
        i = exponent - (prefixExponent = Math.max(-8, Math.min(8, Math.floor(exponent / 3))) * 3) + 1,
        n = coefficient.length;
    return i === n ? coefficient
        : i > n ? coefficient + new Array(i - n + 1).join("0")
        : i > 0 ? coefficient.slice(0, i) + "." + coefficient.slice(i)
        : "0." + new Array(1 - i).join("0") + formatDecimalParts(x, Math.max(0, p + i - 1))[0]; // less than 1y!
  }

  function formatRounded(x, p) {
    var d = formatDecimalParts(x, p);
    if (!d) return x + "";
    var coefficient = d[0],
        exponent = d[1];
    return exponent < 0 ? "0." + new Array(-exponent).join("0") + coefficient
        : coefficient.length > exponent + 1 ? coefficient.slice(0, exponent + 1) + "." + coefficient.slice(exponent + 1)
        : coefficient + new Array(exponent - coefficient.length + 2).join("0");
  }

  var formatTypes = {
    "%": (x, p) => (x * 100).toFixed(p),
    "b": (x) => Math.round(x).toString(2),
    "c": (x) => x + "",
    "d": formatDecimal,
    "e": (x, p) => x.toExponential(p),
    "f": (x, p) => x.toFixed(p),
    "g": (x, p) => x.toPrecision(p),
    "o": (x) => Math.round(x).toString(8),
    "p": (x, p) => formatRounded(x * 100, p),
    "r": formatRounded,
    "s": formatPrefixAuto,
    "X": (x) => Math.round(x).toString(16).toUpperCase(),
    "x": (x) => Math.round(x).toString(16)
  };

  function identity(x) {
    return x;
  }

  var map = Array.prototype.map,
      prefixes = ["y","z","a","f","p","n","µ","m","","k","M","G","T","P","E","Z","Y"];

  function formatLocale(locale) {
    var group = locale.grouping === undefined || locale.thousands === undefined ? identity : formatGroup(map.call(locale.grouping, Number), locale.thousands + ""),
        currencyPrefix = locale.currency === undefined ? "" : locale.currency[0] + "",
        currencySuffix = locale.currency === undefined ? "" : locale.currency[1] + "",
        decimal = locale.decimal === undefined ? "." : locale.decimal + "",
        numerals = locale.numerals === undefined ? identity : formatNumerals(map.call(locale.numerals, String)),
        percent = locale.percent === undefined ? "%" : locale.percent + "",
        minus = locale.minus === undefined ? "−" : locale.minus + "",
        nan = locale.nan === undefined ? "NaN" : locale.nan + "";

    function newFormat(specifier) {
      specifier = formatSpecifier(specifier);

      var fill = specifier.fill,
          align = specifier.align,
          sign = specifier.sign,
          symbol = specifier.symbol,
          zero = specifier.zero,
          width = specifier.width,
          comma = specifier.comma,
          precision = specifier.precision,
          trim = specifier.trim,
          type = specifier.type;

      // The "n" type is an alias for ",g".
      if (type === "n") comma = true, type = "g";

      // The "" type, and any invalid type, is an alias for ".12~g".
      else if (!formatTypes[type]) precision === undefined && (precision = 12), trim = true, type = "g";

      // If zero fill is specified, padding goes after sign and before digits.
      if (zero || (fill === "0" && align === "=")) zero = true, fill = "0", align = "=";

      // Compute the prefix and suffix.
      // For SI-prefix, the suffix is lazily computed.
      var prefix = symbol === "$" ? currencyPrefix : symbol === "#" && /[boxX]/.test(type) ? "0" + type.toLowerCase() : "",
          suffix = symbol === "$" ? currencySuffix : /[%p]/.test(type) ? percent : "";

      // What format function should we use?
      // Is this an integer type?
      // Can this type generate exponential notation?
      var formatType = formatTypes[type],
          maybeSuffix = /[defgprs%]/.test(type);

      // Set the default precision if not specified,
      // or clamp the specified precision to the supported range.
      // For significant precision, it must be in [1, 21].
      // For fixed precision, it must be in [0, 20].
      precision = precision === undefined ? 6
          : /[gprs]/.test(type) ? Math.max(1, Math.min(21, precision))
          : Math.max(0, Math.min(20, precision));

      function format(value) {
        var valuePrefix = prefix,
            valueSuffix = suffix,
            i, n, c;

        if (type === "c") {
          valueSuffix = formatType(value) + valueSuffix;
          value = "";
        } else {
          value = +value;

          // Determine the sign. -0 is not less than 0, but 1 / -0 is!
          var valueNegative = value < 0 || 1 / value < 0;

          // Perform the initial formatting.
          value = isNaN(value) ? nan : formatType(Math.abs(value), precision);

          // Trim insignificant zeros.
          if (trim) value = formatTrim(value);

          // If a negative value rounds to zero after formatting, and no explicit positive sign is requested, hide the sign.
          if (valueNegative && +value === 0 && sign !== "+") valueNegative = false;

          // Compute the prefix and suffix.
          valuePrefix = (valueNegative ? (sign === "(" ? sign : minus) : sign === "-" || sign === "(" ? "" : sign) + valuePrefix;
          valueSuffix = (type === "s" ? prefixes[8 + prefixExponent / 3] : "") + valueSuffix + (valueNegative && sign === "(" ? ")" : "");

          // Break the formatted value into the integer “value” part that can be
          // grouped, and fractional or exponential “suffix” part that is not.
          if (maybeSuffix) {
            i = -1, n = value.length;
            while (++i < n) {
              if (c = value.charCodeAt(i), 48 > c || c > 57) {
                valueSuffix = (c === 46 ? decimal + value.slice(i + 1) : value.slice(i)) + valueSuffix;
                value = value.slice(0, i);
                break;
              }
            }
          }
        }

        // If the fill character is not "0", grouping is applied before padding.
        if (comma && !zero) value = group(value, Infinity);

        // Compute the padding.
        var length = valuePrefix.length + value.length + valueSuffix.length,
            padding = length < width ? new Array(width - length + 1).join(fill) : "";

        // If the fill character is "0", grouping is applied after padding.
        if (comma && zero) value = group(padding + value, padding.length ? width - valueSuffix.length : Infinity), padding = "";

        // Reconstruct the final output based on the desired alignment.
        switch (align) {
          case "<": value = valuePrefix + value + valueSuffix + padding; break;
          case "=": value = valuePrefix + padding + value + valueSuffix; break;
          case "^": value = padding.slice(0, length = padding.length >> 1) + valuePrefix + value + valueSuffix + padding.slice(length); break;
          default: value = padding + valuePrefix + value + valueSuffix; break;
        }

        return numerals(value);
      }

      format.toString = function() {
        return specifier + "";
      };

      return format;
    }

    function formatPrefix(specifier, value) {
      var f = newFormat((specifier = formatSpecifier(specifier), specifier.type = "f", specifier)),
          e = Math.max(-8, Math.min(8, Math.floor(exponent(value) / 3))) * 3,
          k = Math.pow(10, -e),
          prefix = prefixes[8 + e / 3];
      return function(value) {
        return f(k * value) + prefix;
      };
    }

    return {
      format: newFormat,
      formatPrefix: formatPrefix
    };
  }

  var locale;
  var format;

  defaultLocale({
    thousands: ",",
    grouping: [3],
    currency: ["$", ""]
  });

  function defaultLocale(definition) {
    locale = formatLocale(definition);
    format = locale.format;
    return locale;
  }

  function count(node) {
    var sum = 0,
        children = node.children,
        i = children && children.length;
    if (!i) sum = 1;
    else while (--i >= 0) sum += children[i].value;
    node.value = sum;
  }

  function node_count() {
    return this.eachAfter(count);
  }

  function node_each(callback, that) {
    let index = -1;
    for (const node of this) {
      callback.call(that, node, ++index, this);
    }
    return this;
  }

  function node_eachBefore(callback, that) {
    var node = this, nodes = [node], children, i, index = -1;
    while (node = nodes.pop()) {
      callback.call(that, node, ++index, this);
      if (children = node.children) {
        for (i = children.length - 1; i >= 0; --i) {
          nodes.push(children[i]);
        }
      }
    }
    return this;
  }

  function node_eachAfter(callback, that) {
    var node = this, nodes = [node], next = [], children, i, n, index = -1;
    while (node = nodes.pop()) {
      next.push(node);
      if (children = node.children) {
        for (i = 0, n = children.length; i < n; ++i) {
          nodes.push(children[i]);
        }
      }
    }
    while (node = next.pop()) {
      callback.call(that, node, ++index, this);
    }
    return this;
  }

  function node_find(callback, that) {
    let index = -1;
    for (const node of this) {
      if (callback.call(that, node, ++index, this)) {
        return node;
      }
    }
  }

  function node_sum(value) {
    return this.eachAfter(function(node) {
      var sum = +value(node.data) || 0,
          children = node.children,
          i = children && children.length;
      while (--i >= 0) sum += children[i].value;
      node.value = sum;
    });
  }

  function node_sort(compare) {
    return this.eachBefore(function(node) {
      if (node.children) {
        node.children.sort(compare);
      }
    });
  }

  function node_path(end) {
    var start = this,
        ancestor = leastCommonAncestor(start, end),
        nodes = [start];
    while (start !== ancestor) {
      start = start.parent;
      nodes.push(start);
    }
    var k = nodes.length;
    while (end !== ancestor) {
      nodes.splice(k, 0, end);
      end = end.parent;
    }
    return nodes;
  }

  function leastCommonAncestor(a, b) {
    if (a === b) return a;
    var aNodes = a.ancestors(),
        bNodes = b.ancestors(),
        c = null;
    a = aNodes.pop();
    b = bNodes.pop();
    while (a === b) {
      c = a;
      a = aNodes.pop();
      b = bNodes.pop();
    }
    return c;
  }

  function node_ancestors() {
    var node = this, nodes = [node];
    while (node = node.parent) {
      nodes.push(node);
    }
    return nodes;
  }

  function node_descendants() {
    return Array.from(this);
  }

  function node_leaves() {
    var leaves = [];
    this.eachBefore(function(node) {
      if (!node.children) {
        leaves.push(node);
      }
    });
    return leaves;
  }

  function node_links() {
    var root = this, links = [];
    root.each(function(node) {
      if (node !== root) { // Don’t include the root’s parent, if any.
        links.push({source: node.parent, target: node});
      }
    });
    return links;
  }

  function* node_iterator() {
    var node = this, current, next = [node], children, i, n;
    do {
      current = next.reverse(), next = [];
      while (node = current.pop()) {
        yield node;
        if (children = node.children) {
          for (i = 0, n = children.length; i < n; ++i) {
            next.push(children[i]);
          }
        }
      }
    } while (next.length);
  }

  function hierarchy(data, children) {
    if (data instanceof Map) {
      data = [undefined, data];
      if (children === undefined) children = mapChildren;
    } else if (children === undefined) {
      children = objectChildren;
    }

    var root = new Node(data),
        node,
        nodes = [root],
        child,
        childs,
        i,
        n;

    while (node = nodes.pop()) {
      if ((childs = children(node.data)) && (n = (childs = Array.from(childs)).length)) {
        node.children = childs;
        for (i = n - 1; i >= 0; --i) {
          nodes.push(child = childs[i] = new Node(childs[i]));
          child.parent = node;
          child.depth = node.depth + 1;
        }
      }
    }

    return root.eachBefore(computeHeight);
  }

  function node_copy() {
    return hierarchy(this).eachBefore(copyData);
  }

  function objectChildren(d) {
    return d.children;
  }

  function mapChildren(d) {
    return Array.isArray(d) ? d[1] : null;
  }

  function copyData(node) {
    if (node.data.value !== undefined) node.value = node.data.value;
    node.data = node.data.data;
  }

  function computeHeight(node) {
    var height = 0;
    do node.height = height;
    while ((node = node.parent) && (node.height < ++height));
  }

  function Node(data) {
    this.data = data;
    this.depth =
    this.height = 0;
    this.parent = null;
  }

  Node.prototype = hierarchy.prototype = {
    constructor: Node,
    count: node_count,
    each: node_each,
    eachAfter: node_eachAfter,
    eachBefore: node_eachBefore,
    find: node_find,
    sum: node_sum,
    sort: node_sort,
    path: node_path,
    ancestors: node_ancestors,
    descendants: node_descendants,
    leaves: node_leaves,
    links: node_links,
    copy: node_copy,
    [Symbol.iterator]: node_iterator
  };

  function roundNode(node) {
    node.x0 = Math.round(node.x0);
    node.y0 = Math.round(node.y0);
    node.x1 = Math.round(node.x1);
    node.y1 = Math.round(node.y1);
  }

  function treemapDice(parent, x0, y0, x1, y1) {
    var nodes = parent.children,
        node,
        i = -1,
        n = nodes.length,
        k = parent.value && (x1 - x0) / parent.value;

    while (++i < n) {
      node = nodes[i], node.y0 = y0, node.y1 = y1;
      node.x0 = x0, node.x1 = x0 += node.value * k;
    }
  }

  function partition$1() {
    var dx = 1,
        dy = 1,
        padding = 0,
        round = false;

    function partition(root) {
      var n = root.height + 1;
      root.x0 =
      root.y0 = padding;
      root.x1 = dx;
      root.y1 = dy / n;
      root.eachBefore(positionNode(dy, n));
      if (round) root.eachBefore(roundNode);
      return root;
    }

    function positionNode(dy, n) {
      return function(node) {
        if (node.children) {
          treemapDice(node, node.x0, dy * (node.depth + 1) / n, node.x1, dy * (node.depth + 2) / n);
        }
        var x0 = node.x0,
            y0 = node.y0,
            x1 = node.x1 - padding,
            y1 = node.y1 - padding;
        if (x1 < x0) x0 = x1 = (x0 + x1) / 2;
        if (y1 < y0) y0 = y1 = (y0 + y1) / 2;
        node.x0 = x0;
        node.y0 = y0;
        node.x1 = x1;
        node.y1 = y1;
      };
    }

    partition.round = function(x) {
      return arguments.length ? (round = !!x, partition) : round;
    };

    partition.size = function(x) {
      return arguments.length ? (dx = +x[0], dy = +x[1], partition) : [dx, dy];
    };

    partition.padding = function(x) {
      return arguments.length ? (padding = +x, partition) : padding;
    };

    return partition;
  }

  function initRange(domain, range) {
    switch (arguments.length) {
      case 0: break;
      case 1: this.range(domain); break;
      default: this.range(range).domain(domain); break;
    }
    return this;
  }

  const implicit = Symbol("implicit");

  function ordinal() {
    var index = new InternMap(),
        domain = [],
        range = [],
        unknown = implicit;

    function scale(d) {
      let i = index.get(d);
      if (i === undefined) {
        if (unknown !== implicit) return unknown;
        index.set(d, i = domain.push(d) - 1);
      }
      return range[i % range.length];
    }

    scale.domain = function(_) {
      if (!arguments.length) return domain.slice();
      domain = [], index = new InternMap();
      for (const value of _) {
        if (index.has(value)) continue;
        index.set(value, domain.push(value) - 1);
      }
      return scale;
    };

    scale.range = function(_) {
      return arguments.length ? (range = Array.from(_), scale) : range.slice();
    };

    scale.unknown = function(_) {
      return arguments.length ? (unknown = _, scale) : unknown;
    };

    scale.copy = function() {
      return ordinal(domain, range).unknown(unknown);
    };

    initRange.apply(scale, arguments);

    return scale;
  }

  cubehelixLong(cubehelix$1(-100, 0.75, 0.35), cubehelix$1(80, 1.50, 0.8));

  cubehelixLong(cubehelix$1(260, 0.75, 0.35), cubehelix$1(80, 1.50, 0.8));

  var c = cubehelix$1();

  function rainbow(t) {
    if (t < 0 || t > 1) t -= Math.floor(t);
    var ts = Math.abs(t - 0.5);
    c.h = 360 * t - 100;
    c.s = 1.5 - 1.5 * ts;
    c.l = 0.8 - 0.9 * ts;
    return c + "";
  }

  function constant(x) {
    return function constant() {
      return x;
    };
  }

  var abs = Math.abs;
  var atan2 = Math.atan2;
  var cos = Math.cos;
  var max = Math.max;
  var min = Math.min;
  var sin = Math.sin;
  var sqrt = Math.sqrt;

  var epsilon = 1e-12;
  var pi = Math.PI;
  var halfPi = pi / 2;
  var tau = 2 * pi;

  function acos(x) {
    return x > 1 ? 0 : x < -1 ? pi : Math.acos(x);
  }

  function asin(x) {
    return x >= 1 ? halfPi : x <= -1 ? -halfPi : Math.asin(x);
  }

  function arcInnerRadius(d) {
    return d.innerRadius;
  }

  function arcOuterRadius(d) {
    return d.outerRadius;
  }

  function arcStartAngle(d) {
    return d.startAngle;
  }

  function arcEndAngle(d) {
    return d.endAngle;
  }

  function arcPadAngle(d) {
    return d && d.padAngle; // Note: optional!
  }

  function intersect(x0, y0, x1, y1, x2, y2, x3, y3) {
    var x10 = x1 - x0, y10 = y1 - y0,
        x32 = x3 - x2, y32 = y3 - y2,
        t = y32 * x10 - x32 * y10;
    if (t * t < epsilon) return;
    t = (x32 * (y0 - y2) - y32 * (x0 - x2)) / t;
    return [x0 + t * x10, y0 + t * y10];
  }

  // Compute perpendicular offset line of length rc.
  // http://mathworld.wolfram.com/Circle-LineIntersection.html
  function cornerTangents(x0, y0, x1, y1, r1, rc, cw) {
    var x01 = x0 - x1,
        y01 = y0 - y1,
        lo = (cw ? rc : -rc) / sqrt(x01 * x01 + y01 * y01),
        ox = lo * y01,
        oy = -lo * x01,
        x11 = x0 + ox,
        y11 = y0 + oy,
        x10 = x1 + ox,
        y10 = y1 + oy,
        x00 = (x11 + x10) / 2,
        y00 = (y11 + y10) / 2,
        dx = x10 - x11,
        dy = y10 - y11,
        d2 = dx * dx + dy * dy,
        r = r1 - rc,
        D = x11 * y10 - x10 * y11,
        d = (dy < 0 ? -1 : 1) * sqrt(max(0, r * r * d2 - D * D)),
        cx0 = (D * dy - dx * d) / d2,
        cy0 = (-D * dx - dy * d) / d2,
        cx1 = (D * dy + dx * d) / d2,
        cy1 = (-D * dx + dy * d) / d2,
        dx0 = cx0 - x00,
        dy0 = cy0 - y00,
        dx1 = cx1 - x00,
        dy1 = cy1 - y00;

    // Pick the closer of the two intersection points.
    // TODO Is there a faster way to determine which intersection to use?
    if (dx0 * dx0 + dy0 * dy0 > dx1 * dx1 + dy1 * dy1) cx0 = cx1, cy0 = cy1;

    return {
      cx: cx0,
      cy: cy0,
      x01: -ox,
      y01: -oy,
      x11: cx0 * (r1 / r - 1),
      y11: cy0 * (r1 / r - 1)
    };
  }

  function arc() {
    var innerRadius = arcInnerRadius,
        outerRadius = arcOuterRadius,
        cornerRadius = constant(0),
        padRadius = null,
        startAngle = arcStartAngle,
        endAngle = arcEndAngle,
        padAngle = arcPadAngle,
        context = null;

    function arc() {
      var buffer,
          r,
          r0 = +innerRadius.apply(this, arguments),
          r1 = +outerRadius.apply(this, arguments),
          a0 = startAngle.apply(this, arguments) - halfPi,
          a1 = endAngle.apply(this, arguments) - halfPi,
          da = abs(a1 - a0),
          cw = a1 > a0;

      if (!context) context = buffer = path();

      // Ensure that the outer radius is always larger than the inner radius.
      if (r1 < r0) r = r1, r1 = r0, r0 = r;

      // Is it a point?
      if (!(r1 > epsilon)) context.moveTo(0, 0);

      // Or is it a circle or annulus?
      else if (da > tau - epsilon) {
        context.moveTo(r1 * cos(a0), r1 * sin(a0));
        context.arc(0, 0, r1, a0, a1, !cw);
        if (r0 > epsilon) {
          context.moveTo(r0 * cos(a1), r0 * sin(a1));
          context.arc(0, 0, r0, a1, a0, cw);
        }
      }

      // Or is it a circular or annular sector?
      else {
        var a01 = a0,
            a11 = a1,
            a00 = a0,
            a10 = a1,
            da0 = da,
            da1 = da,
            ap = padAngle.apply(this, arguments) / 2,
            rp = (ap > epsilon) && (padRadius ? +padRadius.apply(this, arguments) : sqrt(r0 * r0 + r1 * r1)),
            rc = min(abs(r1 - r0) / 2, +cornerRadius.apply(this, arguments)),
            rc0 = rc,
            rc1 = rc,
            t0,
            t1;

        // Apply padding? Note that since r1 ≥ r0, da1 ≥ da0.
        if (rp > epsilon) {
          var p0 = asin(rp / r0 * sin(ap)),
              p1 = asin(rp / r1 * sin(ap));
          if ((da0 -= p0 * 2) > epsilon) p0 *= (cw ? 1 : -1), a00 += p0, a10 -= p0;
          else da0 = 0, a00 = a10 = (a0 + a1) / 2;
          if ((da1 -= p1 * 2) > epsilon) p1 *= (cw ? 1 : -1), a01 += p1, a11 -= p1;
          else da1 = 0, a01 = a11 = (a0 + a1) / 2;
        }

        var x01 = r1 * cos(a01),
            y01 = r1 * sin(a01),
            x10 = r0 * cos(a10),
            y10 = r0 * sin(a10);

        // Apply rounded corners?
        if (rc > epsilon) {
          var x11 = r1 * cos(a11),
              y11 = r1 * sin(a11),
              x00 = r0 * cos(a00),
              y00 = r0 * sin(a00),
              oc;

          // Restrict the corner radius according to the sector angle.
          if (da < pi && (oc = intersect(x01, y01, x00, y00, x11, y11, x10, y10))) {
            var ax = x01 - oc[0],
                ay = y01 - oc[1],
                bx = x11 - oc[0],
                by = y11 - oc[1],
                kc = 1 / sin(acos((ax * bx + ay * by) / (sqrt(ax * ax + ay * ay) * sqrt(bx * bx + by * by))) / 2),
                lc = sqrt(oc[0] * oc[0] + oc[1] * oc[1]);
            rc0 = min(rc, (r0 - lc) / (kc - 1));
            rc1 = min(rc, (r1 - lc) / (kc + 1));
          }
        }

        // Is the sector collapsed to a line?
        if (!(da1 > epsilon)) context.moveTo(x01, y01);

        // Does the sector’s outer ring have rounded corners?
        else if (rc1 > epsilon) {
          t0 = cornerTangents(x00, y00, x01, y01, r1, rc1, cw);
          t1 = cornerTangents(x11, y11, x10, y10, r1, rc1, cw);

          context.moveTo(t0.cx + t0.x01, t0.cy + t0.y01);

          // Have the corners merged?
          if (rc1 < rc) context.arc(t0.cx, t0.cy, rc1, atan2(t0.y01, t0.x01), atan2(t1.y01, t1.x01), !cw);

          // Otherwise, draw the two corners and the ring.
          else {
            context.arc(t0.cx, t0.cy, rc1, atan2(t0.y01, t0.x01), atan2(t0.y11, t0.x11), !cw);
            context.arc(0, 0, r1, atan2(t0.cy + t0.y11, t0.cx + t0.x11), atan2(t1.cy + t1.y11, t1.cx + t1.x11), !cw);
            context.arc(t1.cx, t1.cy, rc1, atan2(t1.y11, t1.x11), atan2(t1.y01, t1.x01), !cw);
          }
        }

        // Or is the outer ring just a circular arc?
        else context.moveTo(x01, y01), context.arc(0, 0, r1, a01, a11, !cw);

        // Is there no inner ring, and it’s a circular sector?
        // Or perhaps it’s an annular sector collapsed due to padding?
        if (!(r0 > epsilon) || !(da0 > epsilon)) context.lineTo(x10, y10);

        // Does the sector’s inner ring (or point) have rounded corners?
        else if (rc0 > epsilon) {
          t0 = cornerTangents(x10, y10, x11, y11, r0, -rc0, cw);
          t1 = cornerTangents(x01, y01, x00, y00, r0, -rc0, cw);

          context.lineTo(t0.cx + t0.x01, t0.cy + t0.y01);

          // Have the corners merged?
          if (rc0 < rc) context.arc(t0.cx, t0.cy, rc0, atan2(t0.y01, t0.x01), atan2(t1.y01, t1.x01), !cw);

          // Otherwise, draw the two corners and the ring.
          else {
            context.arc(t0.cx, t0.cy, rc0, atan2(t0.y01, t0.x01), atan2(t0.y11, t0.x11), !cw);
            context.arc(0, 0, r0, atan2(t0.cy + t0.y11, t0.cx + t0.x11), atan2(t1.cy + t1.y11, t1.cx + t1.x11), cw);
            context.arc(t1.cx, t1.cy, rc0, atan2(t1.y11, t1.x11), atan2(t1.y01, t1.x01), !cw);
          }
        }

        // Or is the inner ring just a circular arc?
        else context.arc(0, 0, r0, a10, a00, cw);
      }

      context.closePath();

      if (buffer) return context = null, buffer + "" || null;
    }

    arc.centroid = function() {
      var r = (+innerRadius.apply(this, arguments) + +outerRadius.apply(this, arguments)) / 2,
          a = (+startAngle.apply(this, arguments) + +endAngle.apply(this, arguments)) / 2 - pi / 2;
      return [cos(a) * r, sin(a) * r];
    };

    arc.innerRadius = function(_) {
      return arguments.length ? (innerRadius = typeof _ === "function" ? _ : constant(+_), arc) : innerRadius;
    };

    arc.outerRadius = function(_) {
      return arguments.length ? (outerRadius = typeof _ === "function" ? _ : constant(+_), arc) : outerRadius;
    };

    arc.cornerRadius = function(_) {
      return arguments.length ? (cornerRadius = typeof _ === "function" ? _ : constant(+_), arc) : cornerRadius;
    };

    arc.padRadius = function(_) {
      return arguments.length ? (padRadius = _ == null ? null : typeof _ === "function" ? _ : constant(+_), arc) : padRadius;
    };

    arc.startAngle = function(_) {
      return arguments.length ? (startAngle = typeof _ === "function" ? _ : constant(+_), arc) : startAngle;
    };

    arc.endAngle = function(_) {
      return arguments.length ? (endAngle = typeof _ === "function" ? _ : constant(+_), arc) : endAngle;
    };

    arc.padAngle = function(_) {
      return arguments.length ? (padAngle = typeof _ === "function" ? _ : constant(+_), arc) : padAngle;
    };

    arc.context = function(_) {
      return arguments.length ? ((context = _ == null ? null : _), arc) : context;
    };

    return arc;
  }

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
      function getWidth(text, fontSize, fontFace='System') {
          context.font = fontSize + 'px ' + fontFace;
          return context.measureText(text).width;
      }

      return {
          getWidth: getWidth
      };
  })();

  function getFontSize(text, max_width) {
      let name_font_size = 12;
      for (let j = 12; j < 22; j++) {
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
              words.push(e);
          });
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
      name_font_size = Math.max(name_font_size,12);

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
              .text(e);
      });
  }

  function addTextToCircleTop(svg, text_array, diameter, circle_index, x, y, class_name) {
      let words = [];
      for (let i = 0; i < text_array.length; i++) {
          text_array[i].split(' ').forEach(e => {
              words.push(e);
          });
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
              .text(e);
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
      let long_text_width = BrowserText.getWidth(longer_text, name_font_size);

      let rect_padding = 3;
      let rect_width = long_text_width + rect_padding * 2;
      svg.append('rect')
          .attr('width', rect_width + 40)
          .attr('height', name_font_size * text_array.length + rect_padding * 4)
          .attr('x', x - rect_width / 2)
          .attr('y', y - rect_padding - name_font_size / 2)
          .attr("class", class_name);

      let text_element = svg.append('text')
          .attr('x', x - rect_width / 2 + rect_padding)
          .attr('y', y - name_font_size / 2)
          .attr('font-size', name_font_size + 'px')
          .attr("class", class_name);

      text_array.forEach(e => {
          text_element.append('tspan')
              .attr('x', x - rect_width / 2 + rect_padding)
              .attr('dy', name_font_size)
              .attr("class", class_name)
              .text(e);
      });


  }

  function doShowAllConnections(state, flag) {
      state.shadow;
      let rootSvg = state.svg;
      let domain_connections = state.domain_connections;
      let domain_keys = state.domain_keys;


      rootSvg.selectAll(".chord").remove();
      if (!flag) {
          return;
      }


      let connected_circles = [];
      domain_connections.forEach(e => {
          if (domain_keys.hasOwnProperty(e['recipient']) && domain_keys.hasOwnProperty(e['initiator'])) {
              connected_circles.push({
                  'x1': domain_keys[e['recipient']]['cx'],
                  'y1': domain_keys[e['recipient']]['cy'],
                  'x2': domain_keys[e['initiator']]['cx'],
                  'y2': domain_keys[e['initiator']]['cy'],
              });
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
          .style('stroke-width', "2px")
          .style('stroke', "#0362fc")
      ;
  }

  function showMainPage(state) {
      state.svg.selectAll("*").remove();
      sendMessageToMoreInfo({});

      var sorted_domains = state.domains.slice(0);
      sorted_domains.sort(function (a, b) {
          return b.subdomains - a.subdomains;
      });

      let box_size = sorted_domains[0].subdomains;

      let used_cells = createMatrix(box_size, box_size);
      used_cells = copyMatrix([], used_cells);


      //calculate circle coordinates
      for (let i = 0; i < sorted_domains.length; i++) {
          let found_domain_coords = false;
          let subdomains_count = Math.max(3, sorted_domains[i].subdomains);
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
          let diameter = Math.max(3, sorted_domains[i]['subdomains']) * state.size / box_size;
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
                  let key = select(this).attr('data-key');
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
              let ind = select(this).attr('data-i');
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

      let connected_circles = [];
      domain_connections.forEach(e => {
          if (e['initiator'] == select_key || e['recipient'] == select_key) {
              if (domain_keys.hasOwnProperty(e['recipient']) && domain_keys.hasOwnProperty(e['initiator'])) {
                  connected_circles.push({
                      'x1': domain_keys[e['recipient']]['cx'],
                      'y1': domain_keys[e['recipient']]['cy'],
                      'x2': domain_keys[e['initiator']]['cx'],
                      'y2': domain_keys[e['initiator']]['cy'],
                  });
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
          .style('stroke-width', "2px")
          .style('stroke', "#0362fc")
      ;
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
      let width = state.size;
      let key_arr = Object.keys(state.domain_keys);
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


                  addTextToCircleCenter(state.svg, [subdomain['name'], '(' + subdomain['boundedContexts'].length + ')'], 0.75 * r, j, x, y, 'subdomain-circle');

              }


          } else {
              let x = width / 2 + (R + r) * Math.cos(alfa * Math.PI / 180);
              let y = width / 2 + (R + r) * Math.sin(alfa * Math.PI / 180);

              console.log('alfa:' + alfa);
              console.log('key:' + item['key']);
              alfa += delta_alfa;
              let className = 'out-circle';
              if (item['key'] == '000-000') className += ' external-circle';
              state.svg.append("circle")
                  .attr("cx", x)
                  .attr("cy", y)
                  .attr("class", className)
                  .attr("r", r - 5)
                  .attr("data-key", item['key'])
                  .on("mousedown", function () {
                      let key = select(this).attr('data-key');
                      showDomainPageFn(state, key);
                  });

              state.out_circle_coordinates[item['key']] = {'cx': x, 'cy': y};

              addTextToCircleCenter(state.svg, [item['name'], '(' + item['subdomains'] + ')'], R, i, x, y);
          }
      }

      let subdomain_mouseoveredFn = subdomain_mouseovered;
      let subdomain_mouseoutFn = subdomain_mouseout;
      let showSubdomainPageFn = showSubdomainPage;
      let showDomainPageFn = showDomainPage;


      state.svg.selectAll("circle.subdomain-circle")
          .on("mouseover", function () {
              let key = select(this).attr('data-key');
              let cx = select(this).attr('cx');
              let cy = select(this).attr('cy');
              subdomain_mouseoveredFn(state, key, cx, cy);
          })
          .on("mouseout", function () {
              subdomain_mouseoutFn(state);

          })
          .on("mousedown", function () {
              let sel_domain = select(this).attr('data-domain');
              let sel_subdomain = select(this).attr('data-key');
              showSubdomainPageFn(state, sel_domain, sel_subdomain);
              // showDomainPage(key);
          });


      // Object.keys(this.domain_keys).forEach(e => {
      //     console.log(e)
      // });


  }


  function subdomain_mouseovered(state, select_key, x, y) {
      state.svg.selectAll(".chord").remove();

      let connected_circles = [];
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
          .style('stroke-width', "2px")
          .style('stroke', "#0362fc")
      ;
  }

  function subdomain_mouseout(state) {
      state.svg.selectAll(".chord").remove();
  }

  function showSubdomainPage(state, select_domain_key, select_subdomain_key) {
      sendMessageToMoreInfo({
          Domain: select_domain_key,
          SubDomain: select_subdomain_key
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
              };
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
                      by = context_owner_circle['y'];
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
                      let key = select(this).attr('data-key');
                      let cx = select(this).attr('cx');
                      let cy = select(this).attr('cy');
                      boundcontext_mouseovered(state, key, cx, cy);
                  })
                  .on("mouseout", function () {
                      boundcontext_mouseout(state);
                  })
                  .on("mousedown", function () {
                      let select_context_key = select(this).attr('data-key');
                      let select_domain_key = select(this).attr('data-domain');
                      let select_subdomain_key = select(this).attr('data-subdomain');
                      let cx = select(this).attr('cx');
                      let cy = select(this).attr('cy');
                      let owner_x = select(this).attr('owner_x');
                      let owner_y = select(this).attr('owner_y');
                      let owner_r = select(this).attr('owner_r');
                      showBoundedContextConnections(state, select_domain_key, select_subdomain_key, select_context_key, parseFloat(cx), parseFloat(cy), parseFloat(owner_x), parseFloat(owner_y), parseFloat(owner_r));
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
                      let sel_domain = select(this).attr('data-domain');
                      let sel_subdomain = select(this).attr('data-key');
                      showSubdomainPage(state, sel_domain, sel_subdomain);
                      // showDomainPage(key);
                  });

              addTextToCircleCenter(state.svg, [e['name']], (r - 15) * 2, 0, x, y, 'not-select-subdomain');
              alfa += delta_alfa;
          }
      });
      //
  }

  function boundcontext_mouseovered(state, select_key, x, y) {
      state.svg.selectAll(".chord").remove();

      let connected_circles = [];
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
          .style('stroke-width', "2px")
          .style('stroke', "#0362fc")
      ;
  }

  function boundcontext_mouseout(state) {
      state.svg.selectAll(".chord").remove();
  }

  function sendMessageToMoreInfo(message) {
      if (app && app.ports && app.ports.onMoreInfoChanged) {
          app.ports.onMoreInfoChanged.send(JSON.stringify(message));
      }
  }

  function showBoundedContextConnections(state, select_domain_key, select_subdomain_key, select_context_key, x, y, owner_x, owner_y, owner_r) {
      sendMessageToMoreInfo({
          Domain: select_context_key,
          SubDomain: select_subdomain_key,
          BoundedContext: select_context_key
      });

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
                      state.domain_connections.forEach(se => {
                          if (se['initiator'] == select_context_key || se['recipient'] == select_context_key) {
                              let distance = 0;
                              let xc, yc;
                              if (state.out_circle_coordinates.hasOwnProperty(se['recipient'])) {
                                  let it = {
                                      'x1': state.out_circle_coordinates[se['recipient']]['cx'],
                                      'y1': state.out_circle_coordinates[se['recipient']]['cy'],
                                      'x2': select_context_cx,
                                      'y2': select_context_cy,
                                      'name': se['name']
                                  };
                                  connected_circles.push(it);

                                  let desc = '(recipient)';
                                  if (se['name'] !== undefined)
                                      desc = se['name'] + desc;

                                  distance = Math.sqrt(Math.pow(it['x1'] - it['x2'], 2) + Math.pow(it['y1'] - it['y2'], 2));
                                  xc = (it['x1'] + it['x2']) / 2;
                                  yc = (it['y1'] + it['y2']) / 2;

                                  if (!(se['recipient'] in texts)) {
                                      texts[se['recipient']] = {
                                          'distance': distance,
                                          'xc': xc,
                                          'yc': yc,
                                          'text': []
                                      };
                                  }

                                  texts[se['recipient']]['text'].push(desc);

                                  console.log('select_key:' + select_context_key + '\nrecipient:' + se['recipient']);
                              }

                              if (state.out_circle_coordinates.hasOwnProperty(se['initiator'])) {
                                  let it = {
                                      'x1': select_context_cx,
                                      'y1': select_context_cy,
                                      'x2': state.out_circle_coordinates[se['initiator']]['cx'],
                                      'y2': state.out_circle_coordinates[se['initiator']]['cy'],
                                      'name': se['name']
                                  };
                                  connected_circles.push(it);
                                  distance = Math.sqrt(Math.pow(it['x1'] - it['x2'], 2) + Math.pow(it['y1'] - it['y2'], 2));
                                  xc = (it['x1'] + it['x2']) / 2;
                                  yc = (it['y1'] + it['y2']) / 2;

                                  let desc = '(initiator)';
                                  if (se['name'] !== undefined)
                                      desc = se['name'] + desc;

                                  if (!(se['initiator'] in texts)) {
                                      texts[se['initiator']] = {
                                          'distance': distance,
                                          'xc': xc,
                                          'yc': yc,
                                          'text': []
                                      };
                                  }

                                  texts[se['initiator']]['text'].push(desc);
                                  console.log('select_key:' + select_context_key + '\ninitiator:' + se['initiator']);

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
                          .style('stroke-width', "2px")
                          .style('stroke', "#0362fc")
                      ;

                      let key_list = Object.keys(texts);
                      key_list.forEach(k => {
                          addTextToRectangle(state.svg, texts[k]['text'], texts[k]['distance'] / 2, texts[k]['xc'], texts[k]['yc'], 'bound-context-name');
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
                              showBoundedContextConnections(state, select_domain_key, select_subdomain_key, ce['key'], x, y, owner_x, owner_y, owner_r);
                          });


                      addTextToCircleCenter(state.svg, [ce['name']], owner_r / 2, 0, bx, by, 'context-circle-view');


                      context_alfa += context_delta_alfa;
                  }
              });

              state.svg.selectAll("circle.context-circle-view")
                  .on("mouseover", function () {
                      let key = select(this).attr('data-key');
                      let cx = select(this).attr('cx');
                      let cy = select(this).attr('cy');
                      boundcontext_mouseovered(state, key, cx, cy);
                  })
                  .on("mouseout", function () {
                      boundcontext_mouseout(state);
                  });

          }
      });


  }


  function calculateSizeFromHint$2(sizeHint) {
      const width = sizeHint.width;
      const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight);

      return Math.max(Math.min(width, height), 1000)
  }

  function guessWidthAndHeightFromElement$2(element) {
      const parentStyle = window.getComputedStyle(element);

      const width =
          element.clientWidth
          - parseFloat(parentStyle.paddingLeft)
          - parseFloat(parentStyle.paddingRight);
      const maxHeight =
          window.visualViewport.height
          - window.visualViewport.offsetTop
          - element.clientHeight
          - 20
          - parseFloat(parentStyle.paddingTop)
          - parseFloat(parentStyle.paddingBottom)
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
      const svg = select(element)
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
        fill: #f1f1f1;
    }

    circle.out-circle {
        fill: #c2c2c2;
    }
    
    circle.external-circle{
        fill: white !important;
        stroke-dasharray: 4px;
        stroke-width: 2px;
        stroke: #d2d2d2;
    }
    
    circle.main-page{
        fill: #c2c2c2;
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
        fill: #3094ff;
    }
    
    tspan.context-circle{
        fill: black;
    }
    
    circle.context-circle-view{
        fill: #3094ff;
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


    
    .btn-link {
    font-weight: 400;
    color: #0d6efd;
    text-decoration: underline;
}

</style>
`;

  class Bubble extends HTMLElement {
      bubbleState = new BubbleState();

      constructor() {
          super();


          this.bubbleState.rootThis = this;
          this.bubbleState.shadow = this.attachShadow({mode: 'open'});
          this.bubbleState.shadow.appendChild(template.content.cloneNode(true));
      }


      async connectedCallback() {
          this.bubbleState.size = calculateSizeFromHint$2(guessWidthAndHeightFromElement$2(this.parentElement));
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
          console.log('start');
          const baseApi = this.getAttribute('baseApi');

          let show_all_connections = false;
          let domains = [];
          let domain_keys = {};
          let bounded_context_subdomains = {};
          let bounded_context_domains = {};
          let subdomain_domains = {};

          let domain_connections = [];
          let out_circle_coordinates = {};

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
                  };
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
              domains.push(domain_keys[e]);
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

  // ATM wee need to full name reference so the build succeeds

  function mainPorts(app) {
      if (app && app.ports) {
          customElements.define('bubble-visualization', Bubble);
          app.ports.showHome.subscribe(function () {
              const bubble = document.querySelector('bubble-visualization');
              bubble.showMain();
          });
          app.ports.showAllConnections.subscribe(function (flag) {
              const bubble = document.querySelector('bubble-visualization');
              bubble.showAllConnections(flag);
          });

          if (app.ports.storeVisualization && app.ports.onVisualizationChanged) {
              app.ports.storeVisualization.subscribe(function (mode) {
                  localStorage.setItem("domainIndex_visualization", mode);
              });

              const vizualization = localStorage.getItem('domainIndex_visualization');
              app.ports.onVisualizationChanged.send(vizualization || "unknown");
          }
      }
  }

  function selectAllParentDomains(
      domains,
      relevantDomainsIds
  ) {
      const domainDictionary = Array
          .from(domains)
          .reduce((dict, d) => {
              dict[d.id] = d;
              return dict;
          }, {});
      // we need to select all domains relevant for the visualization
      // this is done in iterations, because we might have the following situation:
      // Found Bounded Context -> SubDomain -> SubDomain -> Domain
      // ATM we can only resolve the next parent domain, so a while loop is needed until we reach a fix-point.
      let newDomainIds = [];
      do {
          newDomainIds = Array.from(relevantDomainsIds.values())
              .map((domainId) => {
                  const domain = domainDictionary[domainId];
                  if (
                      domain.parentDomainId &&
                      !relevantDomainsIds.has(domain.parentDomainId)
                  )
                      return domain.parentDomainId;
              })
              .filter((d) => !!d);
          newDomainIds.forEach((domainId) => relevantDomainsIds.add(domainId));
      } while (newDomainIds.length > 0);
      return relevantDomainsIds;
  }

  async function fetchDomains(baseApi) {
      const response = await fetch(`${baseApi}domains`);

      return await response.json();
  }

  async function fetchBoundedContexts(baseApi, query) {
      const response = await fetch(
          `${baseApi}boundedContexts${query || ""}`
      );

      return await response.json();
  }

  async function fetchData(baseApi, query, highlightMode) {
      const filteredContexts = await fetchBoundedContexts(baseApi, query);
      const displayContexts =
          highlightMode
              ? await fetchBoundedContexts(baseApi)
              : filteredContexts;
      const domains = await fetchDomains(baseApi);
      
      const boundedContextsToDisplay = Array
          .from(displayContexts)
          .reduce(
              (dict, bc) => {
                  if (!dict[bc.parentDomainId]) dict[bc.parentDomainId] = [];
                  dict[bc.parentDomainId].push(bc);
                  return dict;
              },
              {}
          );

      const foundBoundedContextIds =
          new Set(Array
              .from(filteredContexts)
              .map(context => context.id)
          );

      const foundDomainIds =
          new Set(Array
              .from(filteredContexts)
              .map(context => context.parentDomainId)
          );


      // starting from the domains of the matched bounded contexts
      const relevantDomainsIds = selectAllParentDomains(
          domains,
          foundDomainIds
      );

      function isRelevantDomain(domain) {
          return relevantDomainsIds.has(domain.id);
      }

      function shouldMapDomain(domain) {
          return highlightMode || isRelevantDomain(domain);
      }

      function mapDomain(domain) {
          // domain.boundedContexts is not filled in subdomains of the domain
          const boundedContexts = boundedContextsToDisplay[domain.id] || [];
          return {
              id: domain.id,
              name: domain.name,
              wasFound: isRelevantDomain(domain),
              children: [
                  ...domain.subdomains.filter(shouldMapDomain).map(mapDomain),
                  ...boundedContexts.map(mapBoundedContext),
              ],
          };
      }

      function mapBoundedContext(boundedContext) {
          return {
              id: boundedContext.id,
              name: boundedContext.name,
              isBoundedContext: true,
              wasFound: foundBoundedContextIds.has(boundedContext.id),
              children: [],
          };
      }

      return {
          name: "Domain Landscape",
          children: domains
              .filter((domain) => !domain.parentDomainId)
              .filter(shouldMapDomain)
              .map(mapDomain),
      };
  }

  function partition(data, radius) {
      // TODO: Weird error in partitioning
      const partitioned = partition$1().size([2 * Math.PI, radius])(
          hierarchy(data || {children: []})
              .sum((d) => (d.children.length >= 1 ? 0 : 1))
              .sort((a, b) => b.value - a.value)
      );

      return partitioned;
  }

  function initializeElements$1(element, {width, height}) {
      //Create the d3 elements
      const svg = select(element)
          .append("svg")
          .attr("width", width)
          .attr("height", height)
          .attr("viewBox", `0 0 ${width} ${height}`);

      const elements =
          svg
              .append("g")
              .attr("transform", `translate(${width / 2} ${height / 2})`)
              .attr("fill-opacity", 0.6);
      const text =
          svg
              .append("g")
              .attr("transform", `translate(${width / 2} ${height / 2})`)
              .attr("pointer-events", "none")
              .attr("text-anchor", "middle")
              .attr("font-size", 10)
              .attr("font-family", "sans-serif");
      return {svg: svg, elements: elements, text: text};
  }

  function guessWidthAndHeightFromElement$1(element) {
      const parentStyle = window.getComputedStyle(element);
      const width =
          element.clientWidth
          - parseFloat(parentStyle.paddingLeft)
          - parseFloat(parentStyle.paddingRight);
      const maxHeight =
          element.clientHeight
          - parseFloat(parentStyle.paddingTop)
          - parseFloat(parentStyle.paddingBottom);
      return {width: width, maxHeight: maxHeight};
  }

  function calculateSizeFromHint$1(sizeHint) {
      const width = sizeHint.width;
      const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight);
      return {
          width: width,
          height: height,
          radius: Math.min(width, height) / 2
      };
  }

  function shortenText(text, width, guessedMaxCharsLookup) {
      let guessedMaxChars = guessedMaxCharsLookup[width];

      function removeLastChar(textContent, length) {
          return textContent.substr(0, length - 4) + "..."
      }

      text.each(function (node) {
          const text = select(this);
          let content = node.data.name;
          // caching & guessing max chars is an performance optimization
          // otherwise calling `text.node().getComputedTextLength()` leads to a lot of expensive layout/rendering calls
          // which will cause a lagging behavior when filtering/resizing the visualization
          // current implementation assumes a constant width between all sunburst-radiants
          if (guessedMaxChars && content.length >= guessedMaxChars) {
              content = removeLastChar(content, guessedMaxChars);
              text.text(content);
          } else {
              while (text.node().getComputedTextLength() > width) {
                  content = removeLastChar(content, content.length);
                  text.text(content);
                  guessedMaxChars = content.length;
              }
          }
      });
      guessedMaxCharsLookup[width] = guessedMaxChars;
  }

  class Sunburst extends HTMLElement {
      // things required by Custom Elements
      constructor() {
          super();
          this.guessedMaxCharsLookup = {};
          this.resizeObserver = new ResizeObserver(entries => {
              const sizeHint = {width: entries[0].contentRect.width, maxHeight: entries[0].contentRect.height};
              this.resize(sizeHint);
          });
      }

      async connectedCallback() {
          this.size = calculateSizeFromHint$1(guessWidthAndHeightFromElement$1(this.parentElement));
          if(this.isConnected) {
              this.rebuildSunburstElements();
              this.resizeObserver.observe(this.parentElement);
              await this.buildSunburst();
          }
      }

      disconnectedCallback() {
          this.resizeObserver.disconnect();
      }

      async attributeChangedCallback() {
          await this.buildSunburst();
      }

      static get observedAttributes() {
          return ['query', 'mode'];
      }

      inHighlightMode() {
          return this.getAttribute('mode') === 'highlighted';
      }

      async buildSunburst() {
          const query = this.getAttribute('query');
          const baseApi = this.getAttribute('baseApi');

          this.data = await fetchData(baseApi, query, this.inHighlightMode());

          this.renderSunburst();
      }

      resize(sizeHint) {
          this.size = calculateSizeFromHint$1(sizeHint);
          this.rebuildSunburstElements();
          this.renderSunburst();
      }

      rebuildSunburstElements() {
          if (this.sunburst) {
              const node = this.sunburst.svg.node();
              if (node)
                  this.removeChild(node);
              delete this.sunburst.elements;
              delete this.sunburst.text;
              delete this.sunburst.svg;
              delete this.sunburst;
          }
          this.sunburst = initializeElements$1(this, this.size);
      }

      renderSunburst() {
          const arc$1 = arc()
              .startAngle((d) => d.x0)
              .endAngle((d) => d.x1)
              .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.005))
              .padRadius(this.size.radius / 2)
              .innerRadius((d) => d.y0)
              .outerRadius((d) => d.y1 - 1);

          const format$1 = format(",d");

          // Building
          const root = partition(this.data, this.size.radius);

          const color = ordinal(
              quantize(rainbow, root.children ? root.children.length + 1 : 0)
          );

          const inHighlightMode = this.inHighlightMode();

          function highlightOpacity(d) {
              if (inHighlightMode) {
                  if (d.data.wasFound) {
                      return d.data.isBoundedContext
                          ? 1.0
                          : 0.7
                  } else {
                      return 0.3;
                  }
              } else {
                  return d.data.isBoundedContext
                      ? 1.0
                      : 0.7;
              }
          }

          const elements = this.sunburst.elements
              .selectAll("a")
              .data(root.descendants().filter((d) => d.depth))
              .join(enter => {
                  const a = enter
                      .append("a");

                  a
                      .attr("target", "_blank")
                      .append("path")
                      .append("title");

                  return a;
              });

          elements
              .attr("xlink:href", d =>
                  d.data.isBoundedContext
                      ? `/boundedContext/${d.data.id}/canvas`
                      : `/domain/${d.data.id}`
              );
          elements
              .select("path")
              .attr("fill", (d) => {
                  while (d.depth > 1) d = d.parent;
                  return color(d.data.name);
              })
              .attr("opacity", highlightOpacity)
              .attr("d", arc$1);
          elements
              .select("title")
              .text(
                  (d) => {
                      const ancestors =
                          d
                              .ancestors()
                              .reverse()
                              .splice(1) // ignore 'root' as name
                              .map((d) => d.data.name)
                              .join("/");
                      if (d.data.isBoundedContext) {
                          return `Bounded Context\n${ancestors}`;
                      } else {
                          return `${ancestors}:\n${format$1(d.value)} Elements`;
                      }
                  });


          this.sunburst.text
              .selectAll("text")
              .data(
                  root
                      .descendants()
                      .filter((d) => d.depth && ((d.y0 + d.y1) / 2) * (d.x1 - d.x0) > 10)
              )
              .join("text")
              .attr("transform", function (d) {
                  const x = (((d.x0 + d.x1) / 2) * 180) / Math.PI;
                  const y = (d.y0 + d.y1) / 2;
                  return `rotate(${
                    x - 90
                }) translate(${y},0) rotate(${x < 180 ? 0 : 180})`;
              })
              .attr("dy", "0.35em")
              .attr("opacity", highlightOpacity)
              .text((d) => d.data.name)
              .call(shortenText, Math.ceil(this.size.radius / (root.height + 1)), this.guessedMaxCharsLookup);
      }
  }

  function calculateSizeFromHint(sizeHint) {
      const width = sizeHint.width;
      const height = Math.min(sizeHint.width, window.innerHeight, sizeHint.maxHeight);
      return {
          diameter: Math.max(Math.min(width, height), 1000)
      };
  }

  function initializeElements(element, {diameter}) {
      console.log('initializeElements');


      // Constants
      const width = diameter;
      const height = width;
      const innerRadius = Math.min(width, height) * 0.5 - 190;
      const outerRadius = innerRadius + 10;

      // Helpers
      const arc$1 = arc().innerRadius(innerRadius).outerRadius(outerRadius);

      const chord = chordDirected()
          .padAngle(10 / innerRadius)
          .sortSubgroups(descending)
          .sortChords(descending);


      // Create SVG element
      const svg = select(element)
          .append("svg")
          .attr("width", width)
          .attr("height", height)
          .attr("viewBox", [-width / 2, -height / 2, width, height]);

      const group = svg
          .append("g")
          .attr("font-size", 10)
          .attr("font-family", "sans-serif")
          .selectAll("g");

      const lines = svg
          .append("g")
          .attr("fill-opacity", 0.75)
          .selectAll("g");


      return {
          svg: svg,
          group: group,
          chord: chord,
          arc: arc$1,
          innerRadius: innerRadius,
          outerRadius: outerRadius,
          lines: lines
      };
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

  // Lazily construct the package hierarchy from class names.
  function packageMatrix(collaborations, domains, existIds) {
      function niceName(name) {
          return name.replaceAll(" ", "-");
      }

      function resolveDomainNames(domainId) {
          if (!domainId) {
              return [];
          }

          const domain = domains.find((domain) => domain.id == domainId);

          return [...resolveDomainNames(domain.parentDomainId), domain.shortName || domain.name]
              .filter((domainName) => domainName)
              .map(niceName);
      }

      function resolveCollaboratorName(collaborator) {
          const boundedContexts = domains
              .map((domain) => domain.boundedContexts)
              .flat();

          if (collaborator.boundedContext) {
              const boundedContext = boundedContexts.find(
                  (boundedContext) => boundedContext.id == collaborator.boundedContext
              );

              if (!boundedContext) {
                  throw new Error(
                      `Could not find a bounded context with id ${collaborator.boundedContext}`
                  );
              }

              const domainNames = resolveDomainNames(
                  boundedContext.parentDomainId
              ).join(".");

              return `${domainNames}.${boundedContext.shortName || boundedContext.name}`;
          }

          if (collaborator.domain) {
              const domain = domains.find(
                  (domain) => domain.id == collaborator.domain
              );

              if (!domain) {
                  throw new Error(
                      `Could not find a domain with id ${collaborator.domain}`
                  );
              }

              const domainNames = resolveDomainNames(domain.parentDomainId).join(
                  "."
              );

              return `${domainNames}.${domain.shortName || domain.name}`;
          }

          if (collaborator.externalSystem) {
              return `externalSystem.${collaborator.externalSystem}`;
          }

          if (collaborator.frontend) {
              return `frontend.${collaborator.frontend}`;
          }

          throw new Error(
              `Could not resolve a name for collaborator ${JSON.stringify(
                collaborator
            )}`
          );
      }

      function unique(names) {
          const obj = {};
          names.forEach((name) => (obj[name] = true));
          return Object.keys(obj);
      }


      const collaborationData = collaborations.map((collaboration) => {
          return {
              source: resolveCollaboratorName(collaboration.initiator),
              target: resolveCollaboratorName(collaboration.recipient),
              value: 1,
          };
      });

      // First step - ignore all domains/bounded contexts that do not define a collaboration
      const collaborationNames = unique(
          collaborations
              .map((collaboration) => [
                  collaboration.initiator,
                  collaboration.recipient,
              ])
              .flat()
              .map(resolveCollaboratorName)
      );

      collaborationNames.sort();

      function buildMatrix(data, names) {
          const index = new Map(names.map((name, i) => [name, i]));

          const matrix = Array.from(index, () => new Array(names.length).fill(0));
          for (const {source, target, value} of data) {
              matrix[index.get(source)][index.get(target)] += value;
          }

          return matrix;
      }


      let matrix = buildMatrix(collaborationData, collaborationNames);

      return {'matrix': matrix, 'names': collaborationNames};
  }


  // Create a class for the element
  class HierarchicalEdge extends HTMLElement {

      constructor() {
          // Always call super first in constructor
          super();

          this.resizeObserver = new ResizeObserver(entries => {
              const sizeHint = {width: entries[0].contentRect.width, maxHeight: entries[0].contentRect.height};
              this.resize(sizeHint);
          });

          this.shadow = this.attachShadow({mode: 'open'});
      }

      async connectedCallback() {
          this.size = calculateSizeFromHint(guessWidthAndHeightFromElement(this.parentElement));
          if(this.isConnected) {
              this.rebuildHierarchicalEdgeElements();
              this.resizeObserver.observe(this.parentElement);
              await this.buildHierarchicalEdge();
          }
      }

      disconnectedCallback() {
          this.resizeObserver.disconnect();
      }

      async attributeChangedCallback() {
          await this.buildHierarchicalEdge();
      }

      static get observedAttributes() {
          return ['query', 'mode'];
      }

      inHighlightMode() {
          return this.getAttribute('mode') === 'highlighted';
      }

      async buildHierarchicalEdge() {
          const query = this.getAttribute('query');
          const baseApi = this.getAttribute('baseApi');

          this.data = await fetchData(baseApi, query, false);

          function find_children_ids(data) {
              let currentIds = [];
              data.children.forEach(e => {
                  let childIds = [];
                  if ('children' in e)
                      childIds = find_children_ids(e);
                  currentIds.push(e.id);
                  childIds.forEach(ce => currentIds.push(ce));
              });
              return currentIds;
          }

          this.existIds = find_children_ids(this.data);
          // console.log(existIds.length)

          const domainResponse = await fetch(`${baseApi}domains`);
          this.domains = await domainResponse.json();


          const collaborationResponse = await fetch(`${baseApi}collaborations`);
          this.collaborations = await collaborationResponse.json();

          function filteredColab(arr, existIds) {
              if (existIds.length == 0)
                  return arr;

              let filteredArr = [];
              for (let i = 0; i < arr.length; i++) {
                  if (arr[i].initiator.boundedContext && existIds.includes(arr[i].initiator.boundedContext)) {
                      filteredArr.push(arr[i]);
                      continue;
                  }

                  if (arr[i].recipient.boundedContext && existIds.includes(arr[i].recipient.boundedContext)) {
                      filteredArr.push(arr[i]);
                      continue;
                  }

                  if (arr[i].initiator.domain && existIds.includes(arr[i].initiator.domain)) {
                      filteredArr.push(arr[i]);
                      continue;
                  }

                  if (arr[i].recipient.domain && existIds.includes(arr[i].recipient.domain)) {
                      filteredArr.push(arr[i]);
                      continue;
                  }
              }

              return filteredArr;
          }

          this.collaborations = filteredColab(this.collaborations, this.existIds);

          this.rebuildHierarchicalEdgeElements();
          this.renderCollaborations();
      }

      resize(sizeHint) {
          this.size = calculateSizeFromHint(sizeHint);
          this.rebuildHierarchicalEdgeElements();
          this.renderCollaborations();
      }

      rebuildHierarchicalEdgeElements() {
          if (this.matrixEdge) {
              this.matrixEdge.svg.selectAll("*").remove();

              delete this.matrixEdge.chord;
              delete this.matrixEdge.arc;
              delete this.matrixEdge.group;
              delete this.matrixEdge.lines;
              delete this.matrixEdge.svg;
              delete this.matrixEdge;
          }
          this.shadow.innerHTML = '';

          this.matrixEdge = initializeElements(this.shadow, this.size);
      }

      renderCollaborations() {
          if (this.collaborations === undefined || this.domains === undefined)
              return;

          let formattedData = packageMatrix(this.collaborations, this.domains, this.existIds);

          let matrix = formattedData['matrix'];
          let names = formattedData['names'];

          const color = ordinal(
              names,
              quantize(rainbow, names.length)
          );

          const chords = this.matrixEdge.chord(matrix);
          const indexDict = {};
          for (let i = 0; i < chords.length; i++) {
              if (!(chords[i].source.index in indexDict)) {
                  indexDict[chords[i].source.index] = '';
              }
              indexDict[chords[i].source.index] += chords[i].target.index + ',';
          }

          const group = this.matrixEdge.group
              .data(chords.groups)
              .join("g");

          const rootNode = this.matrixEdge.svg;
          let mouseoveredFn = this.mouseovered;
          let mouseoutedFn = this.mouseouted;

          group
              .append("path")
              .attr("fill", (d) => color(names[d.index]))
              .attr("d", this.matrixEdge.arc)
              .attr("class", d => "group")
              .attr("index", d => d.index)
              .attr("targetIndex", d => d.index in indexDict ? indexDict[d.index] : '')
              .on("mouseover", function (d) {
                  mouseoveredFn(rootNode, d);
              })
              .on("mouseout", function (d) {
                  mouseoutedFn(rootNode, d);
              });


          group
              .append("text")
              .each((d) => (d.angle = (d.startAngle + d.endAngle) / 2))
              .attr("class", d => "group")
              .attr("index", d => d.index)
              .attr("targetIndex", d => d.index in indexDict ? indexDict[d.index] : '')
              .attr("dy", "0.35em")
              .attr(
                  "transform",
                  (d) => `
            rotate(${(d.angle * 180) / Math.PI - 90})
            translate(${this.matrixEdge.outerRadius + 5})
            ${d.angle > Math.PI ? "rotate(180)" : ""}
          `
              )
              .attr("text-anchor", (d) => (d.angle > Math.PI ? "end" : null))
              .text((d) => names[d.index])
              .on("mouseover", function (d) {
                  mouseoveredFn(rootNode, d);
              })
              .on("mouseout", function (d) {
                  mouseoutedFn(rootNode, d);
              });

          group.append("title").text(
              (d) => `${names[d.index]}
    ${sum(
                chords,
                (c) => (c.source.index === d.index) * c.source.value
            )} outgoing
    ${sum(
                chords,
                (c) => (c.target.index === d.index) * c.source.value
            )} incoming `
          );

          const ribbon = ribbonArrow()
              .radius(this.matrixEdge.innerRadius - 1)
              .padAngle(1 / this.matrixEdge.innerRadius);

          this.matrixEdge.lines
              .data(chords)
              .join("path")
              .style("mix-blend-mode", "multiply")
              .attr("fill", (d) => color(names[d.target.index]))
              .attr("d", ribbon)
              .attr("class", d => "chord")
              .attr("sourceIndex", d => d.source.index)
              .attr("targetIndex", d => d.target.index)
              .append("title")
              .text(
                  (d) =>
                      `${names[d.source.index]} → ${names[d.target.index]} ${
                        d.source.value
                        }`
              )

          ;
      }


      mouseovered(rootNode, d) {
          const index = d.currentTarget.attributes.index.value;
          const targetIndex = d.currentTarget.attributes.targetIndex.value.split(',');
          const targetIndexes = targetIndex.map(function (x) {
              return parseInt(x, 0);
          });

          console.log(index);

          rootNode.selectAll(".chord").style("opacity", .1);
          rootNode.selectAll(`.chord[sourceIndex="${index}"]`).style("opacity", 1);

          rootNode.selectAll("text.group").style("opacity", .1);
          rootNode.selectAll(`text.group[index="${index}"]`).style("opacity", 1);
          targetIndexes.forEach(e => rootNode.selectAll(`text.group[index="${e}"]`).style("opacity", 1));
      }

      mouseouted(rootNode, d) {
          rootNode.selectAll(".chord").style("opacity", 0.75);
          rootNode.selectAll("text.group").style("opacity", 0.75);
      }
  }

  // customElements.define('hierarchical-edge', HierarchicalEdge);

  // ATM wee need to full name reference so the build succeeds

  function searchPorts(app) {
      if (app && app.ports) {
          if (app.ports.storePresentation && app.ports.onPresentationChanged) {
              app.ports.storePresentation.subscribe(function (mode) {
                  localStorage.setItem("search_presentation", mode);
              });

              const searchPresentation = localStorage.getItem('search_presentation');
              app.ports.onPresentationChanged.send(searchPresentation || "unknown");
          }

          if (app.ports.changeQueryString && app.ports.onQueryStringChanged) {
              // see https://github.com/elm/browser/blob/1.0.0/notes/navigation-in-elements.md

              function extractSearchParams(queryString) {
                  const params = new URLSearchParams(queryString);
                  return Array
                      .from(params.entries())
                      .map(([key, value]) => {
                          return {"name": key, "value": value};
                      });
              }

              function asSearchParams(values) {
                  const params = new URLSearchParams();
                  for (const value of values) {
                      params.append(value.name, value.value);
                  }
                  return params.toString();
              }

              function notifyQueryStringChange() {
                  app.ports.onQueryStringChanged.send(JSON.stringify(extractSearchParams(location.search)));
              }

              // Inform app of browser navigation (the BACK and FORWARD buttons)
              window.addEventListener('popstate', function (event) {
                  notifyQueryStringChange();
              });

              // Change the URL upon request, inform app of the change.
              app.ports.changeQueryString.subscribe(function (queryParameters) {
                  const queryString = asSearchParams(JSON.parse(queryParameters));
                  if (new URLSearchParams(location.search).toString() !== queryString) {
                      history.pushState({}, '', queryString.startsWith("?") ? queryString : "?" + queryString);
                      notifyQueryStringChange();
                  }
              });

              // send initial query string to application
              notifyQueryStringChange();
          }


          customElements.define('visualization-sunburst', Sunburst);
          customElements.define('hierarchical-edge', HierarchicalEdge);
      }
  }

  exports.Bubble = Bubble;
  exports.HierarchicalEdge = HierarchicalEdge;
  exports.Sunburst = Sunburst;
  exports.fetchData = fetchData;
  exports.mainPorts = mainPorts;
  exports.searchPorts = searchPorts;

  Object.defineProperty(exports, '__esModule', { value: true });

  return exports;

}({}));
