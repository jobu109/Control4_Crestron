(function(global) {
  "use strict";

  function mount(parent, send) {
    const panel = document.createElement("details");
    panel.className = "rm-devices";
    panel.hidden = true;
    const summary = document.createElement("summary");
    summary.textContent = "Lighting and shades";
    panel.append(summary);
    const controls = new Map();

    function addDevice({id, name, shade}) {
      const special = shade ? [["Open", "open"], ["Close", "close"], ["Stop", "stop"]] :
        [["On", "level", 100], ["Off", "level", 0]];
      const group = document.createElement("fieldset");
      const title = document.createElement("legend");
      title.textContent = name;
      const actual = document.createElement("p");
      actual.setAttribute("role", "status");
      actual.textContent = "Awaiting Control4 feedback";
      group.append(title, actual);
      function button(label, action, value) {
        const control = document.createElement("button");
        control.type = "button";
        control.textContent = label;
        control.onclick = () => send(id, action, typeof value === "function" ? value() : value);
        group.append(control);
      }
      for (const [label, action, value] of special) button(label, action, value);
      const label = document.createElement("label");
      label.textContent = "Target %";
      const target = document.createElement("input");
      target.type = "number";
      target.min = "0";
      target.max = "100";
      target.step = "1";
      target.value = "50";
      label.append(target);
      group.append(label);
      button("Apply", "level", () => target.valueAsNumber);
      group.disabled = true;
      controls.set(id, { group, actual });
      panel.append(group);
    }

    parent.append(panel);

    return function update(connected, devices) {
      panel.hidden = devices.length === 0;
      const ids = new Set(devices.map(device => device.id));
      for (const [id, control] of controls) {
        if (ids.has(id)) continue;
        control.group.remove();
        controls.delete(id);
      }
      for (const device of devices) {
        if (!controls.has(device.id)) addDevice(device);
        const { group, actual } = controls.get(device.id);
        group.disabled = !connected;
        actual.textContent = !connected ? "Control4 disconnected" :
          Number.isInteger(device.value) ? (device.shade ? "Position " : "Level ") + device.value + "%" : "Awaiting Control4 feedback";
      }
    };
  }

  global.PanelRoomDevices = { mount };
})(window);
