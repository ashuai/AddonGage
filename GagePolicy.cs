using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovell.API.Models {
    public class GagePolicy : Policy {
        new Terminal terminal;
        public GagePolicy(Terminal term) {
            type = -1;
            terminal = term;
        }
       
        public new int CheckFoodPrice(int shop = -1, DateTime? date = null) {
            Policy p = null;
            DB.Snapshot(db => {
                p = CheckFoodPrice(db, shop, date);
            });
            if (p.GetError()) return 0;
            else return p.id;
           // return p;
        }
        
        public new Policy CheckFoodPrice(DB db, int shop = -1, DateTime? _date = null) {
            if (terminal == null) throw new ArgumentNullException("[Policy] terminal can't is null");
            if (shop == -1) shop = terminal.shop.id;
            var date = DateTime.Now;
            if (_date != null) date = (DateTime)_date;
            List(db, 41, 1);
            if (list.Count == 0) return this;
            var ok = new List<Policy>();
            if (date == DateTime.MinValue) date = DateTime.Now;
            // throw new Exception("count: " + list.Count);
            for (int i = 0; i < list.Count; i++) {
                if (list[i].date_start > DateTime.MinValue && list[i].date_end > DateTime.MinValue) { // 日期范围
                    switch (DateRangeCheck(date, new[] { list[i].date_start, list[i].date_end })) {
                        case 3: return Error(3, "ifttt date error");
                        case 0: continue;
                        case 1: break; // found
                        default: return Error(3, "ifttt date unknow error");
                    }
                }
                if (list[i].time_start > 0 && list[i].time_end > 0) { // 时间范围
                    switch (TimeRangeCheck(date, new[] { list[i].time_start, list[i].time_end })) {
                        case 3: return Error(3, "ifttt time error");
                        case 0: continue; // not found
                        case 1: break; // found
                        default: return Error(3, "ifttt time unknow error");
                    }
                }
                
                // 星期
                if (!ifttt_week(date,list[i])) continue;

                // 门店
                if (!ifttt_shops(list[i], shop)) continue;

                if (!list[i].term_types.IsEmpty()) { // 终端类型
                    if (0 == list[i].term_types.Split(',').Count(s => s.ToInt() == terminal.type)) continue;
                }
                // 终端
                if (!ifttt_terms(list[i])) continue;
                ok.Add(list[i]);
            }
            //throw new Exception("ok count: " + ok.Count);
            if (ok.Count > 1) return Error(3, "policy too many (" + ok.Select(s => s.id.ToString()).ToArray().Join(","));
            if (ok.Count == 0) return Error(4, "policy not found");
            return ok[0];
        }
        private bool ifttt_week(DateTime now, Policy p) {
            if (p.week.IsEmpty()) return true;
            int w = (int)now.DayOfWeek;
            if (0 < p.week.Split(',').Select(s => s.ToInt() > 6 ? 0 : s.ToInt()).Count(s => s == w)) return true;
            return false;
        }
        private bool ifttt_shops(Policy p,int shop = -1) {
            if (p.shops.IsEmpty()) return true;
            if (shop < 0) shop = terminal.shop.id;
            if (0 < p.shops.Split(',').Count(s => s.ToInt() == shop)) return true;
            return false;
        }
        private bool ifttt_terms(Policy p, int term = -1) {
            if (p.terms.IsEmpty()) return true;
            if (term < 0) term = terminal.id;
            if (0 < p.terms.Split(',').Count(s => s.ToInt() == term)) return true;
            return false;
        }

        private new int DateRangeCheck(DateTime now, DateTime[] date) {
            if (date.Length % 2 != 0) return 3;
            bool hit = false;
            for (int i = 0; i < date.Length; i += 2) {
                if (now.Date >= date[i].Date && now.Date <= date[i + 1].Date) hit = hit || true;
            }
            return hit ? 1 : 0;
        }
        private new int TimeRangeCheck(DateTime now, int[] time) {
            if (time.Length % 2 != 0) return 3;
            int t = now.ToString("HHmm").ToInt();
            for (int i = 0; i < time.Length; i++) {
                //throw new Exception(t+", " + time[i++] + " - " + time[i]);
                if (t.Between(time[i++], time[i])) return 1;
            }
            return 0;
        }
    }
}