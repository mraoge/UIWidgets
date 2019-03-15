using System;
using System.Collections.Generic;
using System.Linq;
using RSG;
using Unity.UIWidgets.animation;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.gestures;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.rendering;
using Unity.UIWidgets.service;
using Unity.UIWidgets.ui;
using Unity.UIWidgets.widgets;
using UnityEngine;
using Canvas = Unity.UIWidgets.ui.Canvas;
using Color = Unity.UIWidgets.ui.Color;
using Rect = Unity.UIWidgets.ui.Rect;
using TextStyle = Unity.UIWidgets.painting.TextStyle;

namespace Unity.UIWidgets.material {
    class DropdownConstants {
        public static readonly TimeSpan _kDropdownMenuDuration = new TimeSpan(0, 0, 0, 0, 300);
        public const float _kMenuItemHeight = 48.0f;
        public const float _kDenseButtonHeight = 24.0f;
        public static EdgeInsets _kMenuItemPadding = EdgeInsets.symmetric(horizontal: 16.0f);
        public static EdgeInsets _kAlignedButtonPadding = EdgeInsets.only(left: 16.0f, right: 4.0f);
        public static EdgeInsets _kUnalignedButtonPadding = EdgeInsets.zero;
        public static EdgeInsets _kAlignedMenuMargin = EdgeInsets.zero;
        public static EdgeInsets _kUnalignedMenuMargin = EdgeInsets.only(left: 16.0f, right: 24.0f);
    }

    class _DropdownMenuPainter : AbstractCustomPainter {
        public _DropdownMenuPainter(
            Color color = null,
            int? elevation = null,
            int? selectedIndex = null,
            Animation<float> resize = null
        ) : base(repaint: resize) {
            D.assert(elevation != null);
            this._painter = new BoxDecoration(
                color: color,
                borderRadius: BorderRadius.circular(2.0f),
                boxShadow: ShadowConstants.kElevationToShadow[elevation ?? 0]
            ).createBoxPainter();
        }

        public readonly Color color;
        public readonly int elevation;
        public readonly int selectedIndex;
        public readonly Animation<float> resize;

        public readonly BoxPainter _painter;

        public override void paint(Canvas canvas, Size size) {
            float selectedItemOffset = this.selectedIndex * DropdownConstants._kMenuItemHeight +
                                       Constants.kMaterialListPadding.top;
            FloatTween top = new FloatTween(
                begin: selectedItemOffset.clamp(0.0f, size.height - DropdownConstants._kMenuItemHeight),
                end: 0.0f
            );

            FloatTween bottom = new FloatTween(
                begin: (top.begin + DropdownConstants._kMenuItemHeight).clamp(DropdownConstants._kMenuItemHeight,
                    size.height),
                end: size.height
            );

            Rect rect = Rect.fromLTRB(0.0f, top.evaluate(this.resize), size.width, bottom.evaluate(this.resize));

            this._painter.paint(canvas, rect.topLeft, new ImageConfiguration(size: rect.size));
        }

        public override bool shouldRepaint(CustomPainter painter) {
            _DropdownMenuPainter oldPainter = painter as _DropdownMenuPainter;
            return oldPainter.color != this.color
                   || oldPainter.elevation != this.elevation
                   || oldPainter.selectedIndex != this.selectedIndex
                   || oldPainter.resize != this.resize;
        }
    }

    class _DropdownScrollBehavior : ScrollBehavior {
        public _DropdownScrollBehavior() {
        }

        public override Widget buildViewportChrome(BuildContext context, Widget child, AxisDirection axisDirection) {
            return child;
        }

        public override ScrollPhysics getScrollPhysics(BuildContext context) {
            return new ClampingScrollPhysics();
        }
    }

    class _DropdownMenu<T> : StatefulWidget where T : Diagnosticable {
        public _DropdownMenu(
            Key key = null,
            EdgeInsets padding = null,
            _DropdownRoute<T> route = null
        ) : base(key: key) {
            this.route = route;
            this.padding = padding;
        }

        public readonly _DropdownRoute<T> route;
        public readonly EdgeInsets padding;

        public override State createState() {
            return new _DropdownMenuState<T>();
        }
    }

    class _DropdownMenuState<T> : State<_DropdownMenu<T>> where T : Diagnosticable {
        CurvedAnimation _fadeOpacity;
        CurvedAnimation _resize;

        public _DropdownMenuState() {
        }

        public override void initState() {
            base.initState();
            // We need to hold these animations as state because of their curve
            // direction. When the route's animation reverses, if we were to recreate
            // the CurvedAnimation objects in build, we'd lose
            // CurvedAnimation._curveDirection.
            this._fadeOpacity = new CurvedAnimation(
                parent: this.widget.route.animation,
                curve: new Interval(0.0f, 0.25f),
                reverseCurve: new Interval(0.75f, 1.0f)
            );
            this._resize = new CurvedAnimation(
                parent: this.widget.route.animation,
                curve: new Interval(0.25f, 0.5f),
                reverseCurve: new Threshold(0.0f)
            );
        }

        public override Widget build(BuildContext context) {
            // The menu is shown in three stages (unit timing in brackets):
            // [0s - 0.25s] - Fade in a rect-sized menu container with the selected item.
            // [0.25s - 0.5s] - Grow the otherwise empty menu container from the center
            //   until it's big enough for as many items as we're going to show.
            // [0.5s - 1.0fs] Fade in the remaining visible items from top to bottom.
            //
            // When the menu is dismissed we just fade the entire thing out
            // in the first 0.25s.
            D.assert(MaterialD.debugCheckHasMaterialLocalizations(context));
            MaterialLocalizations localizations = MaterialLocalizations.of(context);
            _DropdownRoute<T> route = this.widget.route;
            float unit = 0.5f / (route.items.Count + 1.5f);
            List<Widget> children = new List<Widget>();
            for (int itemIndex = 0; itemIndex < route.items.Count; ++itemIndex) {
                CurvedAnimation opacity;
                if (itemIndex == route.selectedIndex) {
                    opacity = new CurvedAnimation(parent: route.animation, curve: new Threshold(0.0f));
                }
                else {
                    float start = (0.5f + (itemIndex + 1) * unit).clamp(0.0f, 1.0f);
                    float end = (start + 1.5f * unit).clamp(0.0f, 1.0f);
                    opacity = new CurvedAnimation(parent: route.animation, curve: new Interval(start, end));
                }

                children.Add(new FadeTransition(
                    opacity: opacity,
                    child: new InkWell(
                        child: new Container(
                            padding: this.widget.padding,
                            child: route.items[itemIndex]
                        ),
                        onTap: () => Navigator.pop(
                            context,
                            new _DropdownRouteResult<T>(route.items[itemIndex].value)
                        )
                    )
                ));
            }

            return new FadeTransition(
                opacity: this._fadeOpacity,
                child: new CustomPaint(
                    painter: new _DropdownMenuPainter(
                        color: Theme.of(context).canvasColor,
                        elevation: route.elevation,
                        selectedIndex: route.selectedIndex,
                        resize: this._resize
                    ),
                    child: new Material(
                        type: MaterialType.transparency,
                        textStyle: route.style,
                        child: new ScrollConfiguration(
                            behavior: new _DropdownScrollBehavior(),
                            child: new Scrollbar(
                                child: new ListView(
                                    controller: this.widget.route.scrollController,
                                    padding: Constants.kMaterialListPadding,
                                    itemExtent: DropdownConstants._kMenuItemHeight,
                                    shrinkWrap: true,
                                    children: children
                                )
                            )
                        )
                    )
                )
            );
        }
    }

    class _DropdownMenuRouteLayout<T> : SingleChildLayoutDelegate {
        public _DropdownMenuRouteLayout(
            Rect buttonRect,
            float menuTop,
            float menuHeight,
            TextDirection textDirection
        ) {
        }

        public readonly Rect buttonRect;
        public readonly float menuTop;
        public readonly float menuHeight;
        public readonly TextDirection textDirection;

        public override BoxConstraints getConstraintsForChild(BoxConstraints constraints) {
            float maxHeight = Mathf.Max(0.0f, constraints.maxHeight - 2 * DropdownConstants._kMenuItemHeight);
            float width = Mathf.Min(constraints.maxWidth, this.buttonRect.width);
            return new BoxConstraints(
                minWidth: width,
                maxWidth: width,
                minHeight: 0.0f,
                maxHeight: maxHeight
            );
        }

        public override Offset getPositionForChild(Size size, Size childSize) {
            D.assert(() => {
                Rect container = Offset.zero & size;
                if (container.intersect(this.buttonRect) == this.buttonRect) {
                    // If the button was entirely on-screen, then verify
                    // that the menu is also on-screen.
                    // If the button was a bit off-screen, then, oh well.
                    D.assert(this.menuTop >= 0.0f);
                    D.assert(this.menuTop + this.menuHeight <= size.height);
                }

                return true;
            });
            D.assert(this.textDirection != null);
            float left;
            switch (this.textDirection) {
                case TextDirection.rtl:
                    left = this.buttonRect.right.clamp(0.0f, size.width) - childSize.width;
                    break;
                case TextDirection.ltr:
                    left = this.buttonRect.left.clamp(0.0f, size.width - childSize.width);
                    break;
                default:
                    throw new Exception("Unknown text direction: " + this.textDirection);
            }

            return new Offset(left, this.menuTop);
        }

        public override bool shouldRelayout(SingleChildLayoutDelegate _oldDelegate) {
            _DropdownMenuRouteLayout<T> oldDelegate = _oldDelegate as _DropdownMenuRouteLayout<T>;
            return this.buttonRect != oldDelegate.buttonRect
                   || this.menuTop != oldDelegate.menuTop
                   || this.menuHeight != oldDelegate.menuHeight
                   || this.textDirection != oldDelegate.textDirection;
        }
    }

    class _DropdownRouteResult<T> {
        public _DropdownRouteResult(T result) {
            this.result = result;
        }

        public readonly T result;

        public static bool operator ==(_DropdownRouteResult<T> left, dynamic right) {
            if (!(right is _DropdownRouteResult<T>)) {
                return false;
            }

            _DropdownRouteResult<T> typedOther = right as _DropdownRouteResult<T>;
            return left.result.Equals(typedOther.result);
        }

        public static bool operator !=(_DropdownRouteResult<T> left, dynamic right) {
            return !(left == right);
        }

        public override int GetHashCode() {
            return this.result.GetHashCode();
        }
    }

    class _DropdownRoute<T> : PopupRoute where T : Diagnosticable {
        public _DropdownRoute(
            List<DropdownMenuItem<T>> items = null,
            EdgeInsets padding = null,
            Rect buttonRect = null,
            int? selectedIndex = null,
            int elevation = 8,
            ThemeData theme = null,
            TextStyle style = null,
            string barrierLabel = null
        ) {
            D.assert(style != null);
            this.items = items;
            this.padding = padding;
            this.buttonRect = buttonRect;
            this.selectedIndex = selectedIndex;
            this.elevation = elevation;
            this.theme = theme;
            this.style = style;
            this.barrierLabel = barrierLabel;
        }

        public readonly List<DropdownMenuItem<T>> items;
        public readonly EdgeInsets padding;
        public readonly Rect buttonRect;
        public readonly int? selectedIndex;
        public readonly int elevation;
        public readonly ThemeData theme;
        public readonly TextStyle style;

        public ScrollController scrollController;

        public override TimeSpan transitionDuration {
            get { return DropdownConstants._kDropdownMenuDuration; }
        }

        public override bool barrierDismissible {
            get { return true; }
        }

        public override Color barrierColor {
            get { return null; }
        }

        public string barrierLabel;

        public override Widget buildPage(BuildContext context, Animation<float> animation,
            Animation<float> secondaryAnimation) {
            D.assert(WidgetsD.debugCheckHasDirectionality(context));
            float screenHeight = MediaQuery.of(context).size.height;
            float maxMenuHeight = screenHeight - 2.0f * DropdownConstants._kMenuItemHeight;

            float buttonTop = this.buttonRect.top;
            float buttonBottom = this.buttonRect.bottom;

            float topLimit = Mathf.Min(DropdownConstants._kMenuItemHeight, buttonTop);
            float bottomLimit = Mathf.Max(screenHeight - DropdownConstants._kMenuItemHeight, buttonBottom);

            float? selectedItemOffset = this.selectedIndex * DropdownConstants._kMenuItemHeight +
                                        Constants.kMaterialListPadding.top;

            float? menuTop = (buttonTop - selectedItemOffset) -
                             (DropdownConstants._kMenuItemHeight - this.buttonRect.height) / 2.0f;
            float preferredMenuHeight = (this.items.Count * DropdownConstants._kMenuItemHeight) +
                                        Constants.kMaterialListPadding.vertical;

            float menuHeight = Mathf.Min(maxMenuHeight, preferredMenuHeight);

            float? menuBottom = menuTop + menuHeight;

            if (menuTop < topLimit) {
                menuTop = Mathf.Min(buttonTop, topLimit);
            }

            if (menuBottom > bottomLimit) {
                menuBottom = Mathf.Max(buttonBottom, bottomLimit);
                menuTop = menuBottom - menuHeight;
            }

            if (this.scrollController == null) {
                float scrollOffset = preferredMenuHeight > maxMenuHeight
                    ? Mathf.Max(0.0f, selectedItemOffset - (buttonTop - menuTop) ?? 0.0f)
                    : 0.0f;
                this.scrollController = new ScrollController(initialScrollOffset: scrollOffset);
            }

            TextDirection textDirection = Directionality.of(context);
            Widget menu = new _DropdownMenu<T>(
                route: this,
                padding: this.padding
            );

            if (this.theme != null) {
                menu = new Theme(data: this.theme, child: menu);
            }

            return MediaQuery.removePadding(
                context: context,
                removeTop: true,
                removeBottom: true,
                removeLeft: true,
                removeRight: true,
                child: new Builder(
                    builder: (BuildContext _context) => {
                        return new CustomSingleChildLayout(
                            layoutDelegate: new _DropdownMenuRouteLayout<T>(
                                buttonRect: this.buttonRect,
                                menuTop: menuTop ?? 0.0f,
                                menuHeight: menuHeight,
                                textDirection: textDirection
                            ),
                            child: menu
                        );
                    }
                )
            );
        }

        public void _dismiss() {
            this.navigator?.removeRoute(this);
        }
    }

    public class DropdownMenuItem<T> : StatelessWidget where T : Diagnosticable {
        /// Creates an item for a dropdown menu.
        ///
        /// The [child] argument is required.
        public DropdownMenuItem(
            Key key = null,
            T value = null,
            Widget child = null
        ) : base(key: key) {
            D.assert(child != null);
            this.value = value;
            this.child = child;
        }

        public readonly Widget child;

        public readonly T value;

        public override Widget build(BuildContext context) {
            return new Container(
                height: DropdownConstants._kMenuItemHeight,
                alignment: Alignment.centerLeft,
                child: this.child
            );
        }
    }

    public class DropdownButtonHideUnderline : InheritedWidget {
        /// Creates a [DropdownButtonHideUnderline]. A non-null [child] must
        /// be given.
        public DropdownButtonHideUnderline(
            Key key = null,
            Widget child = null
        ) : base(key: key, child: child) {
            D.assert(child != null);
        }

        public static bool at(BuildContext context) {
            return context.inheritFromWidgetOfExactType(typeof(DropdownButtonHideUnderline)) != null;
        }

        public override bool updateShouldNotify(InheritedWidget oldWidget) {
            return false;
        }
    }

    public class DropdownButton<T> : StatefulWidget where T : Diagnosticable {
        public DropdownButton(
            Key key = null,
            List<DropdownMenuItem<T>> items = null,
            T value = null,
            Widget hint = null,
            Widget disabledHint = null,
            ValueChanged<T> onChanged = null,
            int elevation = 8,
            TextStyle style = null,
            float iconSize = 24.0f,
            bool isDense = false,
            bool isExpanded = false
        ) :
            base(key: key) {
            D.assert(items == null || value == null ||
                     items.Where<DropdownMenuItem<T>>((DropdownMenuItem<T> item) => item.value.Equals(value)).ToList()
                         .Count == 1);
        }

        public readonly List<DropdownMenuItem<T>> items;

        public readonly T value;

        public readonly Widget hint;

        public readonly Widget disabledHint;

        public readonly ValueChanged<T> onChanged;

        public readonly int elevation;

        public readonly TextStyle style;

        public readonly float iconSize;

        public readonly bool isDense;

        public readonly bool isExpanded;

        public override State createState() {
            return new _DropdownButtonState<T>();
        }
    }

    class _DropdownButtonState<T> : State<DropdownButton<T>>, WidgetsBindingObserver where T : Diagnosticable {
        int? _selectedIndex;
        _DropdownRoute<T> _dropdownRoute;

        public void didChangeTextScaleFactor() {
        }

        public void didChangeLocales(List<Locale> locale) {
        }

        public IPromise<bool> didPopRoute() {
            return Promise<bool>.Resolved(false);
        }

        public IPromise<bool> didPushRoute(string route) {
            return Promise<bool>.Resolved(false);
        }

        public override void initState() {
            base.initState();
            this._updateSelectedIndex();
            WidgetsBinding.instance.addObserver(this);
        }

        public override void dispose() {
            WidgetsBinding.instance.removeObserver(this);
            this._removeDropdownRoute();
            base.dispose();
        }

        public void didChangeMetrics() {
            this._removeDropdownRoute();
        }

        void _removeDropdownRoute() {
            this._dropdownRoute?._dismiss();
            this._dropdownRoute = null;
        }

        public override void didUpdateWidget(StatefulWidget oldWidget) {
            base.didUpdateWidget(oldWidget);
            this._updateSelectedIndex();
        }

        void _updateSelectedIndex() {
            if (!this._enabled) {
                return;
            }

            D.assert(this.widget.value == null ||
                     this.widget.items.Where((DropdownMenuItem<T> item) => item.value.Equals(this.widget.value))
                         .ToList().Count == 1);
            this._selectedIndex = null;
            for (int itemIndex = 0; itemIndex < this.widget.items.Count; itemIndex++) {
                if (this.widget.items[itemIndex].value.Equals(this.widget.value)) {
                    this._selectedIndex = itemIndex;
                    return;
                }
            }
        }

        TextStyle _textStyle {
            get { return this.widget.style ?? Theme.of(this.context).textTheme.subhead; }
        }

        void _handleTap() {
            RenderBox itemBox = (RenderBox) this.context.findRenderObject();
            Rect itemRect = itemBox.localToGlobal(Offset.zero) & itemBox.size;
            TextDirection textDirection = Directionality.of(this.context);
            EdgeInsets menuMargin = ButtonTheme.of(this.context).alignedDropdown
                ? DropdownConstants._kAlignedMenuMargin
                : DropdownConstants._kUnalignedMenuMargin;

            D.assert(this._dropdownRoute == null);
            this._dropdownRoute = new _DropdownRoute<T>(
                items: this.widget.items,
                buttonRect: menuMargin.inflateRect(itemRect),
                padding: DropdownConstants._kMenuItemPadding,
                selectedIndex: this._selectedIndex ?? 0,
                elevation: this.widget.elevation,
                theme: Theme.of(this.context, shadowThemeOnly: true),
                style: this._textStyle,
                barrierLabel: MaterialLocalizations.of(this.context).modalBarrierDismissLabel
            );

            Navigator.push(this.context, this._dropdownRoute).Then(newValue => {
                _DropdownRouteResult<T> value = newValue as _DropdownRouteResult<T>;
                this._dropdownRoute = null;
                if (!this.mounted || newValue == null) {
                    return null;
                }

                if (this.widget.onChanged != null) {
                    this.widget.onChanged(value.result);
                }

                return null;
            });
        }

        float? _denseButtonHeight {
            get {
                return Mathf.Max(this._textStyle.fontSize ?? 0.0f,
                    Mathf.Max(this.widget.iconSize, DropdownConstants._kDenseButtonHeight));
            }
        }

        Color _downArrowColor {
            get {
                if (this._enabled) {
                    if (Theme.of(this.context).brightness == Brightness.light) {
                        return Colors.grey.shade700;
                    }
                    else {
                        return Colors.white70;
                    }
                }
                else {
                    if (Theme.of(this.context).brightness == Brightness.light) {
                        return Colors.grey.shade400;
                    }
                    else {
                        return Colors.white10;
                    }
                }
            }
        }

        bool _enabled {
            get { return this.widget.items != null && this.widget.items.isNotEmpty() && this.widget.onChanged != null; }
        }

        public override Widget build(BuildContext context) {
            D.assert(MaterialD.debugCheckHasMaterial(context));
            D.assert(MaterialD.debugCheckHasMaterialLocalizations(context));

            // The width of the button and the menu are defined by the widest
            // item and the width of the hint.
            List<Widget> items = this._enabled ? new List<Widget>(this.widget.items) : new List<Widget>();
            int hintIndex = 0;
            if (this.widget.hint != null || (!this._enabled && this.widget.disabledHint != null)) {
                Widget emplacedHint =
                    this._enabled
                        ? this.widget.hint
                        : new DropdownMenuItem<Widget>(child: this.widget.disabledHint ?? this.widget.hint);
                hintIndex = items.Count;
                items.Add(new DefaultTextStyle(
                    style: this._textStyle.copyWith(color: Theme.of(context).hintColor),
                    child: new IgnorePointer(
                        child: emplacedHint
                    )
                ));
            }

            EdgeInsets padding = ButtonTheme.of(context).alignedDropdown
                ? DropdownConstants._kAlignedButtonPadding
                : DropdownConstants._kUnalignedButtonPadding;

            IndexedStack innerItemsWidget = new IndexedStack(
                index: this._enabled ? (this._selectedIndex ?? hintIndex) : hintIndex,
                alignment: Alignment.centerLeft,
                children: items
            );

            Widget result = new DefaultTextStyle(
                style: this._textStyle,
                child: new Container(
                    padding: padding,
                    height: this.widget.isDense ? this._denseButtonHeight : null,
                    child: new Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        mainAxisSize: MainAxisSize.min,
                        children: new List<Widget> {
                            this.widget.isExpanded ? new Expanded(child: innerItemsWidget) : (Widget) innerItemsWidget,
                            new Icon(Icons.arrow_drop_down,
                                size: this.widget.iconSize,
                                color: this._downArrowColor
                            )
                        }
                    )
                )
            );

            if (!DropdownButtonHideUnderline.at(context)) {
                float bottom = this.widget.isDense ? 0.0f : 8.0f;
                result = new Stack(
                    children: new List<Widget> {
                        result,
                        new Positioned(
                            left: 0.0f,
                            right: 0.0f,
                            bottom: bottom,
                            child: new Container(
                                height: 1.0f,
                                decoration: new BoxDecoration(
                                    border: new Border(
                                        bottom: new BorderSide(color: new Color(0xFFBDBDBD), width: 0.0f))
                                )
                            )
                        )
                    }
                );
            }

            return new GestureDetector(
                onTap: this._enabled ? (GestureTapCallback) this._handleTap : null,
                behavior: HitTestBehavior.opaque,
                child: result
            );
        }
    }

    public class DropdownButtonFormField<T> : FormField<T> where T : Diagnosticable {
        public DropdownButtonFormField(
            Key key = null,
            T value = null,
            List<DropdownMenuItem<T>> items = null,
            ValueChanged<T> onChanged = null,
            InputDecoration decoration = null,
            FormFieldSetter<T> onSaved = null,
            FormFieldValidator<T> validator = null,
            Widget hint = null
        ) : base(
            key: key,
            onSaved: onSaved,
            initialValue: value,
            validator: validator,
            builder: (FormFieldState<T> field) => {
                InputDecoration effectiveDecoration = (decoration ?? new InputDecoration())
                    .applyDefaults(Theme.of(field.context).inputDecorationTheme);
                return new InputDecorator(
                    decoration: effectiveDecoration.copyWith(errorText: field.errorText),
                    isEmpty: value == null,
                    child: new DropdownButtonHideUnderline(
                        child: new DropdownButton<T>(
                            isDense: true,
                            value: value,
                            items: items,
                            hint: hint,
                            onChanged: field.didChange
                        )
                    )
                );
            }
        ) {
            this.onChanged = onChanged;
        }

        /// Called when the user selects an item.
        public readonly ValueChanged<T> onChanged;

        public State createState() {
            return new _DropdownButtonFormFieldState<T>();
        }
    }

    class _DropdownButtonFormFieldState<T> : FormFieldState<T> where T : Diagnosticable {
        public DropdownButtonFormField<T> widget {
            get { return base.widget as DropdownButtonFormField<T>; }
        }

        public override void didChange(T value) {
            base.didChange(value);
            if (this.widget.onChanged != null) {
                this.widget.onChanged(value);
            }
        }
    }
}