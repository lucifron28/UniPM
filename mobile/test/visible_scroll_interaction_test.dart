import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../integration_test/support/visible_scroll_interaction.dart';

void main() {
  for (final viewport in <({String profile, Size size})>[
    (profile: 'API 29 logical viewport', size: Size(360, 640)),
    (profile: 'API 34 logical viewport', size: Size(411, 891)),
  ]) {
    testWidgets(
      'reveals and taps a lazy offscreen control at ${viewport.profile}',
      (tester) async {
        _setLogicalViewport(tester, viewport.size);
        var tapped = false;

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: ListView.builder(
                itemExtent: 72,
                itemCount: 40,
                itemBuilder: (context, index) => SizedBox(
                  height: 72,
                  child: index == 31
                      ? FilledButton(
                          key: const Key('lazy-offscreen-action'),
                          onPressed: () => tapped = true,
                          child: const Text('Open lazy target'),
                        )
                      : Text('Item $index'),
                ),
              ),
            ),
          ),
        );

        final target = find.byKey(const Key('lazy-offscreen-action'));
        expect(target, findsNothing);
        await VisibleScrollInteraction.tapAfterScrolling(
          tester,
          target,
          'lazy offscreen action',
        );

        expect(tapped, isTrue);
      },
    );
  }

  testWidgets('uses the owning nested scrollable for a lazy target', (
    tester,
  ) async {
    _setLogicalViewport(tester, const Size(360, 640));
    final outerController = ScrollController();
    final innerController = ScrollController();
    addTearDown(outerController.dispose);
    addTearDown(innerController.dispose);
    var tapped = false;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            controller: outerController,
            child: Column(
              children: [
                const SizedBox(height: 12),
                SizedBox(
                  height: 320,
                  child: ListView.builder(
                    key: const Key('nested-list'),
                    controller: innerController,
                    itemExtent: 56,
                    itemCount: 30,
                    itemBuilder: (context, index) => SizedBox(
                      height: 56,
                      child: index == 24
                          ? TextButton(
                              key: const Key('nested-lazy-action'),
                              onPressed: () => tapped = true,
                              child: const Text('Nested target'),
                            )
                          : Text('Nested item $index'),
                    ),
                  ),
                ),
                const SizedBox(height: 700),
              ],
            ),
          ),
        ),
      ),
    );

    final nestedList = find.byKey(const Key('nested-list'));
    final owningScrollable = find.descendant(
      of: nestedList,
      matching: find.byType(Scrollable),
    );
    expect(owningScrollable, findsOneWidget);
    await VisibleScrollInteraction.tapAfterScrolling(
      tester,
      find.byKey(const Key('nested-lazy-action')),
      'nested lazy action',
      scrollable: owningScrollable,
    );

    expect(tapped, isTrue);
    expect(innerController.offset, greaterThan(0));
    expect(outerController.offset, 0);
  });

  testWidgets('retains a lazy-list position across a pushed route and return', (
    tester,
  ) async {
    _setLogicalViewport(tester, const Size(360, 640));
    final controller = ScrollController();
    addTearDown(controller.dispose);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ListView.builder(
            controller: controller,
            itemExtent: 68,
            itemCount: 40,
            itemBuilder: (context, index) => SizedBox(
              height: 68,
              child: index == 27
                  ? TextButton(
                      key: const Key('open-retained-route'),
                      onPressed: () => Navigator.of(context).push<void>(
                        MaterialPageRoute<void>(
                          builder: (context) => Scaffold(
                            body: Center(
                              child: TextButton(
                                onPressed: () => Navigator.of(context).pop(),
                                child: const Text('Return to list'),
                              ),
                            ),
                          ),
                        ),
                      ),
                      child: const Text('Open detail route'),
                    )
                  : Text('Retained row $index'),
            ),
          ),
        ),
      ),
    );

    await VisibleScrollInteraction.tapAfterScrolling(
      tester,
      find.byKey(const Key('open-retained-route')),
      'route control in the retained lazy list',
    );
    await tester.pumpAndSettle();
    expect(find.text('Return to list'), findsOneWidget);
    final retainedOffset = controller.offset;
    expect(retainedOffset, greaterThan(0));

    await tester.tap(find.text('Return to list'));
    await tester.pumpAndSettle();

    expect(controller.offset, closeTo(retainedOffset, 0.01));
    expect(find.byKey(const Key('open-retained-route')), findsOneWidget);
    VisibleScrollInteraction.verifyTapReady(
      tester,
      find.byKey(const Key('open-retained-route')),
      'retained route control after navigation return',
    );
  });

  testWidgets('taps the visible part of a control clipped by the screen edge', (
    tester,
  ) async {
    _setLogicalViewport(tester, const Size(360, 640));
    var tapped = false;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Stack(
            clipBehavior: Clip.none,
            children: [
              Positioned(
                top: 620,
                left: 80,
                width: 200,
                height: 80,
                child: FilledButton(
                  key: const Key('screen-clipped-action'),
                  onPressed: () => tapped = true,
                  child: const Text('Partially on screen'),
                ),
              ),
            ],
          ),
        ),
      ),
    );

    final target = find.byKey(const Key('screen-clipped-action'));
    final visible = VisibleScrollInteraction.clippedBounds(tester, target);
    expect(visible.height, greaterThan(0));
    expect(visible.height, lessThan(tester.getRect(target).height));
    await VisibleScrollInteraction.tapVisibleOnScreen(
      tester,
      target,
      'screen-clipped action',
    );

    expect(tapped, isTrue);
  });
}

void _setLogicalViewport(WidgetTester tester, Size size) {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = size;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}
