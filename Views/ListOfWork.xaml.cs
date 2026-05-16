using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using MissionTime.Models;
using MissionTime.Services;

namespace MissionTime.Views
{
    public partial class ListOfWork : Window
    {
        private readonly DbService _db;

        public ListOfWork(DbService db)
        {
            InitializeComponent();
            _db = db;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var dt = _db.ListOfWork_List();
                var allItems = dt.AsEnumerable().Select(r => new WorkItem
                {
                    Id = r.Field<long>("Id"),
                    ParentId = r.Field<long?>("ParentId"),
                    Name = r.Field<string>("Name"),
                    SpecialCode = r.Field<string>("SpecialCode")
                }).ToList();

                // Собираем дерево
                var root = allItems.Where(i => i.ParentId == null).ToList();
                foreach (var r in root) FillChildren(r, allItems);

                tvWorks.ItemsSource = root;
            }
            catch (Exception ex)
            {
                MissionMessageBox.Show(this, "Ошибка", "Ошибка загрузки: " + ex.Message);
            }
        }

        private void FillChildren(WorkItem parent, List<WorkItem> all)
        {
            var children = all.Where(i => i.ParentId == parent.Id).ToList();
            foreach (var child in children)
            {
                parent.Children.Add(child);
                FillChildren(child, all);
            }
        }

        // Вспомогательный метод для получения выделенного элемента
        private WorkItem GetSelectedWork()
        {
            return FindSelected(tvWorks.ItemsSource as List<WorkItem>);
        }

        private WorkItem FindSelected(List<WorkItem> items)
        {
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item.IsSelected) return item;
                var found = FindSelected(item.Children);
                if (found != null) return found;
            }
            return null;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedWork();
            // Передаем null, если хотим создать корневой элемент, 
            // или Id выбранного, если хотим создать подпункт
            var win = new ListOfWork_Edit(_db, null, selected?.Id);
            win.Owner = this;
            win.ShowInTaskbar = false;

            if (win.ShowDialog() == true) LoadData();
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedWork();
            if (selected == null) return;

            var win = new ListOfWork_Edit(_db, selected, null);
            win.Owner = this;
            win.ShowInTaskbar = false;

            if (win.ShowDialog() == true) LoadData();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedWork();
            if (selected == null) return;

            // Добавляем == true, чтобы превратить bool? в bool
            if (MissionMessageBox.Show(this, "Удаление", $"Вы уверены, что хотите удалить '{selected.Name}'?", true) == true)
            {
                try
                {
                    _db.Work_Delete(selected.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MissionMessageBox.Show(this, "Ошибка", ex.Message);
                }
            }
        }
    }
}