import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';

/// Scrolls test UI through the scrollable that owns the target and taps only
/// after checking the target's actually painted, clipped region.
class VisibleScrollInteraction {
  const VisibleScrollInteraction._();

  static Future<void> scrollUntilVisible(
    WidgetTester tester,
    Finder target, {
    required String description,
    Finder? scrollable,
    bool towardEnd = true,
    int maxScrolls = 24,
  }) async {
    final selectedScrollable = _resolveScrollable(
      tester,
      target,
      explicitScrollable: scrollable,
    );
    final viewport = _viewportFor(tester, selectedScrollable);
    await _bringViewportIntoReach(tester, viewport);

    final viewportBounds = tester.getRect(viewport);
    final visibleViewport = clippedBounds(tester, viewport);
    if (viewportBounds.height <= 0 || visibleViewport.height <= 0) {
      fail(
        'The owning scroll viewport for $description is not visible. '
        'Viewport: $viewportBounds; clipped viewport: $visibleViewport; '
        'scrollable: ${_describeScrollable(tester, selectedScrollable)}.',
      );
    }

    var effectiveTowardEnd = towardEnd;
    if (target.evaluate().length == 1) {
      final targetBounds = tester.getRect(target);
      if (targetBounds.bottom <= visibleViewport.top) {
        effectiveTowardEnd = false;
      } else if (targetBounds.top >= visibleViewport.bottom) {
        effectiveTowardEnd = true;
      }
    }
    final delta = visibleViewport.height * 0.55;

    final scrollableWidget = tester.widget<Scrollable>(selectedScrollable);
    final dragSign = switch (scrollableWidget.axisDirection) {
      AxisDirection.down => effectiveTowardEnd ? -1.0 : 1.0,
      AxisDirection.up => effectiveTowardEnd ? 1.0 : -1.0,
      _ => throw StateError('The selected scrollable is not vertical.'),
    };
    final scrollableState = tester.state<ScrollableState>(selectedScrollable);
    final lastError = <String>[];
    for (var attempt = 0; attempt <= maxScrolls; attempt++) {
      final matches = target.evaluate().toList(growable: false);
      if (matches.length > 1) {
        fail(
          '$description must identify one widget while scrolling; '
          'found ${matches.length}.',
        );
      }
      if (matches.length == 1) {
        final targetRenderObject = matches.single.renderObject;
        if (targetRenderObject == null) {
          fail('$description has no render object after it was built.');
        }
        try {
          await scrollableState.position.ensureVisible(
            targetRenderObject,
            alignment: 0.5,
            duration: Duration.zero,
          );
        } catch (error) {
          lastError.add(error.toString().replaceAll('\n', ' '));
          break;
        }
        await tester.pump();
        verifyTapReady(tester, target, description);
        return;
      } else if (attempt < maxScrolls) {
        await tester.timedDrag(
          selectedScrollable,
          Offset(0, dragSign * delta),
          const Duration(milliseconds: 50),
        );
        await tester.pump();
      }
    }
    fail(
      'Could not reveal $description after $maxScrolls bounded scrolls. '
      'Target matches: ${target.evaluate().length}; '
      'owning scrollable: ${_describeScrollable(tester, selectedScrollable)}; '
      'viewport: $viewportBounds; clipped viewport: $visibleViewport; '
      'render viewport: ${_renderViewportBounds(tester)}; '
      'last positioning errors: ${lastError.join(' | ')}',
    );
  }

  static Future<void> tapAfterScrolling(
    WidgetTester tester,
    Finder target,
    String description, {
    Finder? scrollable,
    bool towardEnd = true,
    int maxScrolls = 24,
  }) async {
    await scrollUntilVisible(
      tester,
      target,
      description: description,
      scrollable: scrollable,
      towardEnd: towardEnd,
      maxScrolls: maxScrolls,
    );
    await tester.tapAt(verifyTapReady(tester, target, description));
  }

  static Future<void> tapVisibleOnScreen(
    WidgetTester tester,
    Finder target,
    String description,
  ) async {
    await tester.tapAt(verifyTapReady(tester, target, description));
  }

  static void verifyVisible(
    WidgetTester tester,
    Finder target,
    String description,
  ) {
    final matches = target.evaluate().toList(growable: false);
    if (matches.length != 1) {
      fail(
        '$description must match one built widget; found ${matches.length}.',
      );
    }
    final bounds = tester.getRect(target);
    final visible = clippedBounds(tester, target);
    if (visible.isEmpty || !visible.contains(bounds.center)) {
      fail(
        '$description is not fully visible at its center. '
        'Target: $bounds; clipped onscreen region: $visible; '
        'render viewport: ${_renderViewportBounds(tester)}.',
      );
    }
  }

  /// Returns the target bounds intersected with every paint clip in its render
  /// ancestor chain and the Flutter render view.
  static Rect clippedBounds(WidgetTester tester, Finder target) {
    final matches = target.evaluate().toList(growable: false);
    if (matches.length != 1) {
      fail(
        'Expected one target to calculate clipped bounds; found ${matches.length}.',
      );
    }
    final targetElement = matches.single;
    final targetRenderObject = targetElement.renderObject;
    if (targetRenderObject == null || !targetRenderObject.attached) {
      fail(
        'Cannot calculate clipped bounds for a target without an attached render object.',
      );
    }

    var visible = tester
        .getRect(target)
        .intersect(_renderViewportBounds(tester));
    RenderObject child = targetRenderObject;
    RenderObject? ancestor = child.parent;
    while (ancestor != null && !visible.isEmpty) {
      final paintClip = ancestor.describeApproximatePaintClip(child);
      if (paintClip != null) {
        final clipInGlobalCoordinates = MatrixUtils.transformRect(
          ancestor.getTransformTo(null),
          paintClip,
        );
        visible = visible.intersect(clipInGlobalCoordinates);
      }
      child = ancestor;
      ancestor = ancestor.parent;
    }
    return visible;
  }

  /// Verifies a point inside both the target and its real clipped region hits
  /// the target's render subtree. This is the point used by the tap helpers.
  static Offset verifyTapReady(
    WidgetTester tester,
    Finder target,
    String description,
  ) {
    final matches = target.evaluate().toList(growable: false);
    if (matches.length != 1) {
      fail(
        '$description must be built exactly once before interaction; '
        'found ${matches.length}.',
      );
    }
    final targetBounds = tester.getRect(target);
    final visible = clippedBounds(tester, target);
    if (visible.isEmpty) {
      fail(
        '$description has no visible painted area. Target: $targetBounds; '
        'clipped onscreen region: $visible; '
        'render viewport: ${_renderViewportBounds(tester)}.',
      );
    }
    final point = visible.center;
    if (!targetBounds.contains(point)) {
      fail(
        '$description has no valid point inside both its bounds and its '
        'clipped onscreen region. Target: $targetBounds; visible: $visible.',
      );
    }

    final hitTest = tester.hitTestOnBinding(point);
    var targetWasHit = false;
    void inspectTargetSubtree(Element element) {
      if (element is RenderObjectElement &&
          hitTest.path.any(
            (entry) => identical(entry.target, element.renderObject),
          )) {
        targetWasHit = true;
      }
      element.visitChildren(inspectTargetSubtree);
    }

    inspectTargetSubtree(matches.single);
    if (!targetWasHit) {
      fail(
        '$description is painted at $point but does not receive the hit test. '
        'Target: $targetBounds; clipped onscreen region: $visible; '
        'hit-test path length: ${hitTest.path.length}.',
      );
    }
    return point;
  }

  static Finder _resolveScrollable(
    WidgetTester tester,
    Finder target, {
    Finder? explicitScrollable,
  }) {
    if (explicitScrollable != null) {
      return _validateScrollable(tester, explicitScrollable);
    }

    final matches = target.evaluate().toList(growable: false);
    if (matches.length == 1) {
      final owner = Scrollable.maybeOf(matches.single, axis: Axis.vertical);
      if (owner != null) return find.byWidget(owner.widget);
    }
    return _activeVerticalScrollable(tester);
  }

  static Finder _validateScrollable(WidgetTester tester, Finder finder) {
    final matches = finder.evaluate().toList(growable: false);
    if (matches.length != 1 || matches.single.widget is! Scrollable) {
      fail(
        'The requested scrollable must identify exactly one Scrollable; '
        'found ${matches.length}.',
      );
    }
    final scrollable = matches.single.widget as Scrollable;
    if (scrollable.axisDirection != AxisDirection.down &&
        scrollable.axisDirection != AxisDirection.up) {
      fail(
        'The requested scrollable is ${scrollable.axisDirection}, not vertical.',
      );
    }
    return find.byWidget(scrollable);
  }

  static Finder _activeVerticalScrollable(WidgetTester tester) {
    final scaffolds = find.byType(Scaffold).evaluate().toList(growable: false);
    if (scaffolds.isEmpty) {
      fail(
        'Cannot find an active Scaffold to resolve the scrollable for a lazy target.',
      );
    }
    final activeScaffold = find.byWidget(scaffolds.last.widget);
    final candidates = find
        .descendant(
          of: activeScaffold,
          matching: find.byWidgetPredicate(
            (widget) =>
                widget is Scrollable &&
                (widget.axisDirection == AxisDirection.down ||
                    widget.axisDirection == AxisDirection.up),
          ),
        )
        .evaluate()
        .toList(growable: false);
    if (candidates.length != 1) {
      final candidateNames = candidates
          .map((element) {
            final widget = element.widget as Scrollable;
            return '${widget.runtimeType}(${widget.axisDirection})';
          })
          .join(', ');
      fail(
        'A lazy target has no built ancestor to identify its owning '
        'scrollable. Expected one vertical Scrollable under the active '
        'Scaffold but found ${candidates.length} [$candidateNames]. Pass the '
        'owning Scrollable finder when the page has nested vertical lists.',
      );
    }
    return find.byWidget(candidates.single.widget);
  }

  static Finder _viewportFor(WidgetTester tester, Finder scrollable) {
    final viewports = _viewportElements(scrollable);
    if (viewports.isEmpty) {
      fail(
        'Could not find a RenderAbstractViewport under the selected Scrollable: '
        '${_describeScrollable(tester, scrollable)}.',
      );
    }
    // A Scrollable builds its own viewport before any nested scrollables, so
    // the first viewport is the one that owns this scroll position.
    return find.byWidget(viewports.first.widget);
  }

  static Future<void> _bringViewportIntoReach(
    WidgetTester tester,
    Finder viewport,
  ) async {
    var bounds = tester.getRect(viewport);
    var visible = clippedBounds(tester, viewport);
    if (!visible.contains(bounds.center)) {
      await Scrollable.ensureVisible(
        tester.element(viewport),
        alignment: 0.5,
        duration: Duration.zero,
      );
      await tester.pump();
      bounds = tester.getRect(viewport);
      visible = clippedBounds(tester, viewport);
    }
    if (!visible.contains(bounds.center)) {
      fail(
        'The selected scroll viewport cannot receive a drag because its '
        'center remains clipped. Viewport: $bounds; visible: $visible; '
        'render viewport: ${_renderViewportBounds(tester)}.',
      );
    }
  }

  static Rect _renderViewportBounds(WidgetTester tester) =>
      Offset.zero & tester.binding.renderViews.single.size;

  static String _describeScrollable(WidgetTester tester, Finder finder) {
    if (finder.evaluate().length != 1) {
      return 'matches=${finder.evaluate().length}';
    }
    final widget = tester.widget<Scrollable>(finder);
    final viewport = _viewportForDescription(finder);
    final bounds = viewport == null
        ? 'unknown'
        : tester.getRect(viewport).toString();
    return '${widget.runtimeType}(${widget.axisDirection}) viewport=$bounds';
  }

  static Finder? _viewportForDescription(Finder scrollable) {
    final viewports = _viewportElements(scrollable);
    if (viewports.isEmpty) return null;
    return find.byWidget(viewports.first.widget);
  }

  static List<Element> _viewportElements(Finder scrollable) => find
      .descendant(
        of: scrollable,
        matching: find.byElementPredicate(
          (element) => element.renderObject is RenderAbstractViewport,
          description: 'RenderAbstractViewport',
        ),
      )
      .evaluate()
      .toList(growable: false);
}
