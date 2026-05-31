window.storylineGraph = {
  attachNodeDrag(selector, dotNetRef) {
    const svg = document.querySelector(selector);
    if (!svg || svg.dataset.dragReady === "1") {
      return;
    }

    svg.dataset.dragReady = "1";
    let active = null;
    let offset = { x: 0, y: 0 };

    const pointFromEvent = (event) => {
      const point = svg.createSVGPoint();
      point.x = event.clientX;
      point.y = event.clientY;
      return point.matrixTransform(svg.getScreenCTM().inverse());
    };

    svg.addEventListener("pointerdown", (event) => {
      const node = event.target.closest(".graph-node");
      if (!node) {
        return;
      }

      active = node;
      const current = pointFromEvent(event);
      const transform = active.transform.baseVal.consolidate();
      const matrix = transform ? transform.matrix : svg.createSVGMatrix();
      offset = { x: current.x - matrix.e, y: current.y - matrix.f };
      active.setPointerCapture(event.pointerId);
    });

    svg.addEventListener("pointermove", async (event) => {
      if (!active) {
        return;
      }

      const current = pointFromEvent(event);
      const x = current.x - offset.x;
      const y = current.y - offset.y;
      active.setAttribute("transform", `translate(${x} ${y})`);
      await dotNetRef.invokeMethodAsync("MoveNode", active.dataset.nodeId, x, y);
    });

    svg.addEventListener("pointerup", () => {
      active = null;
    });

    svg.addEventListener("pointercancel", () => {
      active = null;
    });
  }
};
