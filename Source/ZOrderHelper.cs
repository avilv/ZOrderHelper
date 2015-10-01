using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace avilv.Windows.Forms
{
    [ProvideProperty("ZOrder", typeof(Control))]
    [ToolboxItem(true)]
    public class ZOrderHelper : Component, IExtenderProvider
    {
        private readonly Dictionary<Control, ZOrderManager> _zOrderManagers
            = new Dictionary<Control, ZOrderManager>();

        private readonly Dictionary<Control, ZOrderedControlEntry> _zOrderedControls
            = new Dictionary<Control, ZOrderedControlEntry>();

        public ZOrderHelper(IContainer container)
        {
            if (container == null)
            {
                throw new ArgumentNullException("container");
            }
            container.Add(this);
        }

        public bool CanExtend(object extendee)
        {
            return (((extendee is Control) && !(extendee is Form)));
        }

        [Description("Control's ZOrder")]
        [DefaultValue(-1)]
        [Category("Appearance")]
        public int GetZOrder(Control c)
        {
            ZOrderedControlEntry controlEntry;
            if (_zOrderedControls.TryGetValue(c, out controlEntry))
                return controlEntry.ZOrder;

            return -1;
        }


        public void SetZOrder(Control c, int value)
        {
            if (value == -1)
            {
                RemoveControl(c);
                return;
            }

            ZOrderedControlEntry controlEntry;
            if (!_zOrderedControls.TryGetValue(c, out controlEntry))
            {
                controlEntry = new ZOrderedControlEntry(c);
                _zOrderedControls.Add(c, controlEntry);

                if (c.Parent != null)
                    AssignZOrderManager(controlEntry, c.Parent);

                c.ParentChanged += OnZOrderedControlParentChanged;
                c.Disposed += OnZOrderedControlDisposed;
            }

            controlEntry.ZOrder = value;
        }

        private void RemoveControl(Control control)
        {
            ZOrderedControlEntry controlEntry;
            if (!_zOrderedControls.TryGetValue(control, out controlEntry)) return;

            control.ParentChanged -= OnZOrderedControlParentChanged;
            control.Disposed -= OnZOrderedControlDisposed;

            if (controlEntry.Manager != null)
            {
                controlEntry.Manager.ZOrderedControls.Remove(controlEntry);
                if (controlEntry.Manager.ZOrderedControls.Count == 0)
                    RemoveManager(controlEntry.Manager);
            }

            _zOrderedControls.Remove(control);
        }

        private void RemoveManager(ZOrderManager manager)
        {
            _zOrderManagers.Remove(manager.Container);
            manager.Dispose();
        }
        void OnZOrderedControlDisposed(object sender, EventArgs e)
        {
            var control = (sender as Control);
            if (control == null) return;

            RemoveControl(control);
        }

        private void AssignZOrderManager(ZOrderedControlEntry entry, Control container)
        {
            if (container == null)
            {
                entry.Manager = null;
                return;
            }

            if (entry.Manager != null && entry.Manager.Container == container)
                return;

            ZOrderManager manager;
            if (!_zOrderManagers.TryGetValue(container, out manager))
            {
                manager = new ZOrderManager(container);
                _zOrderManagers.Add(container, manager);
            }

            entry.Manager = manager;
        }

        void OnZOrderedControlParentChanged(object sender, EventArgs e)
        {
            var control = sender as Control;

            if (control == null)
            {
                Debug.Assert(false, "ParentChanged - Control is null");
                return;
            }

            ZOrderedControlEntry controlEntry;
            if (_zOrderedControls.TryGetValue(control, out controlEntry))
            {
                AssignZOrderManager(controlEntry, control.Parent);
            }
            else
            {
                Debug.Assert(false, "ParentChanged - could not find zordered control");
                return;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var controlList = _zOrderedControls.Select(k => k.Key).ToList();
                controlList.ForEach(RemoveControl);
            }
            base.Dispose(disposing);
        }

        private class ZOrderManager : IDisposable
        {
            public Control Container { get; private set; }
            public List<ZOrderedControlEntry> ZOrderedControls { get; private set; }

            public ZOrderManager(Control container)
            {
                this.ZOrderedControls = new List<ZOrderedControlEntry>();

                this.Container = container;
                this.Container.Layout += containerControl_Layout;
            }

            void containerControl_Layout(object sender, LayoutEventArgs e)
            {
                UpdateZOrder();
            }

            private bool _updating = false;
            public bool UpdateZOrder()
            {
                if (_updating) return false;
                _updating = true;

                try
                {

                    // sort controls by zorder
                    var sortedControls = ZOrderedControls.OrderByDescending(k => k.ZOrder).Select(k => k.Control).ToList();


                    // get relevant controls
                    var currentControlsOrder =
                        this.Container.Controls.Cast<Control>().Intersect(sortedControls).ToList();

                    // skip if already ordered
                    if (currentControlsOrder.SequenceEqual(sortedControls)) return false;

                    this.Container.SuspendLayout();

                    for (var i = 0; i < sortedControls.Count; i++)
                    {
                        var control = sortedControls[i];
                        this.Container.Controls.SetChildIndex(control, i);
                    }

                    this.Container.ResumeLayout();
                }
                finally
                {
                    _updating = false;
                }
                return true;
            }
            public void Dispose()
            {
                ZOrderedControls.Clear();
                this.Container.Layout -= containerControl_Layout;
                this.Container = null;
            }
        }

        private class ZOrderedControlEntry
        {
            private ZOrderManager _manager;
            private int _zOrder;
            public Control Control { get; private set; }
            public ZOrderManager Manager
            {
                get { return _manager; }
                set
                {
                    if (_manager != value)
                    {
                        if (_manager != null)
                        {
                            _manager.ZOrderedControls.Remove(this);
                            _manager.UpdateZOrder();
                        }

                        _manager = value;

                        if (_manager != null)
                        {
                            _manager.ZOrderedControls.Add(this);
                            _manager.UpdateZOrder();
                        }
                    }

                }
            }

            public int ZOrder
            {
                get { return _zOrder; }
                set
                {
                    if (_zOrder != value)
                    {
                        _zOrder = value;
                        if (Manager != null)
                            Manager.UpdateZOrder();
                    }
                }
            }

            public ZOrderedControlEntry(Control control)
            {
                this.Control = control;
            }
        }
    }
}