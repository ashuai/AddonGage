using Sovell.ShopAPI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sovell.API.Models {
    public class Gage : BaseModel<Gage> {
        new Terminal terminal;
        public Gage() {
        }
        public Gage(Terminal t) {
            terminal = t;
        }
        #region pay
        public Card card { get; private set; }
        public long seq { get; private set; }
        public Gage GetCardByUID(DB db, string cid, string cno) {
            card = null;
            card = new Card(cid, cno, terminal);
            card.app = "dish";
            card.param.account = "-1";
            if (card.Get(db).GetError(out err)) return Error();
            if (card.state < 1) return Error(10);
            return this;
        }
        //账户 0 主账户 1补贴  ；金额；余额；uid；类型 卡付1 附卡 21
        public List<Tuple<int, ulong, long, string, int>> payment = new List<Tuple<int, ulong, long, string, int>>();
        public Gage Pay(DB db,string cid, ulong amt, ulong amt_dues, string invoice, int part, string pays,string prodstr, int gage =0) {
            if (Exists(db, invoice)) return Error(42, "invoice");
            var major = new Major(db, terminal);
            seq = major.Seq;
            var now = new DateTime(seq);
            //取消费规则 new int[]{ 1,0 };
            var account_rank = ((string)terminal.properties.GetValue("account_rank", "")).Split(",").Select(s => s.ToInt(-5)).ToArray();
            //卡付
            if (cardmix(db, card, account_rank, amt,amt_dues, invoice, major).GetError(out err)) return Error();
            //支付方式
            string[] pay_arry = pays.Split(',');
            if (pay_arry.Length < 5) return Error(3, "pays error");
            if (pay_arry[0].ToInt() != 1 ) return Error(3, "pays error type not 1"); 
            if(pay_arry[1].ToMoney() != amt) return Error(3, "pays error amt not equal");
            if (cid != pay_arry[3]) return Error(3, "pays error cid not equal");
            if (dish_trade_payment_db(db, cid).GetError()) return Error();
            //dish_trade_details 明细
            if (FillProduct(amt,amt_dues, prodstr, gage).GetError()) return Error();
            dish_trade_details_db(db, Products.ToArray());
            //dish_trade 主单据
            string[] times = new string[] { now.ToString(), now.ToString() };
            var hours = GetTimes(part, db);
            if (hours != null) times = hours.Item1.Split(",");
            dish_trade_db(db, card, seq, Products.Count, amt, amt_dues, part, times, (long)card.balance - (long)card.credited, invoice, now, remark: remark, gage: gage);
            return this;
        }
        private List<DishProduct> Products = new List<DishProduct>();
        private Gage FillProduct(ulong amt, ulong amt_dues, string prodstr, int gage) {
            double disc = amt_dues == 0 ? 0 : Math.Round(((double)amt / (double)amt_dues), 10);
            var prods = prodstr.Split(DB.NEWLINE);
            // TODO
            //for (int i = 0; i < prods.Length; i++) {
            //    var pp = prods[i].Split(",");
            //    if (pp.Length < (gage == 1 ? 8 : 7)) return Error(3, 1, "invalid prods (type,id,name,price,cate,wind,uid）│");
            //    if (!pp[6].IsEmpty() && !pp[6].test("^[A-Fa-f0-9]*$")) return Error(3, 2, "invalid prods (type,id,name,price,cate,wind,uid）│");
            //    Products.Add(new DishProduct(pp[0].ToInt(1)) {
            //        id = pp[1], // 1
            //        name = Uri.UnescapeDataString(pp[2]), // 2
            //        price = (long)pp[3].ToMoney(), // 3
            //        amt= (long)(((long)pp[3].ToMoney()) * disc),
            //        //amt = (long)pp[3].ToMoney(), // 3
            //        cate = pp[4].ToInt(), // 4
            //        window = pp[5].ToInt(), // 5
            //        uid = pp[6],
            //        book = (gage == 1 && pp.Length > 7)? pp[7].ToInt():0 //借用book字段实现weight
            //    });
            //}
            if ((ulong)Products.Sum(m =>(float) m.price) != amt_dues) return Error(3, 0, "prods price not equal amt_dues");
            ulong prod_amts = (ulong)Products.Sum(m => (float)m.amt);
            if (prod_amts != amt) {
                // TODO
                // Products[Products.Count - 1].amt += (long)amt - (long)prod_amts;
            }
            if ((ulong)Products.Sum(m => (float)m.amt) != amt) return Error(3, 0, "prods price not equal amt");
            return this;
        }
        Dictionary<int, Tuple<string, int, int>> _times = null;
        private Tuple<string, int, int> GetTimes(int id, DB db = null) {
            if (null != _times) return _times.ContainsKey(id) ? _times[id] : null;
            if (db == null) return null;
            _times = new Dictionary<int, Tuple<string, int, int>>();
            DateTime now = DateTime.Now;
            string str = "";
            int t1, t2, t0, t01;
            if (null != terminal && null != terminal.shop && terminal.shop.id != 1) {
                str = db.GetConfig($"shop_hours_{terminal.shop.id}").Trim();
            }
            if (str.IsEmpty()) str = db.GetConfig("shop_hours").Trim();
            if (str.IsEmpty()) return null;
            var json = str.ToJDictArray();
            int nextday = 0;
            t0 = now.ToString("HHmm").ToInt();
            str = json[0]["stime"].Tostring();
            t01 = str.Replace(":", "").ToInt();
            if (t0 < t01) now = now.AddDays(-1);
            string date = now.ToShortDateString() + " ";
            for (int i = 0; i < 4; i++) {
                str = json[i]["stime"].Tostring();
                t1 = str.Replace(":", "").ToInt();
                str = json[i]["etime"].Tostring();
                t2 = str.Replace(":", "").ToInt();
                if (t1 > t2) {
                    if (nextday > 0) throw new ArgumentOutOfRangeException("[GetTimes] shop hours out of range");
                    nextday += 1;
                    date = now.AddDays(1).ToShortDateString() + " ";
                    _times[i + 1] = Tuple.Create(new string[] { now.ToShortDateString() + " " + json[i]["stime"], date + json[i]["etime"] }.Join(","), t1, t2);
                    continue;
                }
                _times[i + 1] = Tuple.Create(new string[] { date + json[i]["stime"], date + json[i]["etime"] }.Join(","), t1, t2);
            }
            return _times.ContainsKey(id) ? _times[id] : null;
        }
        private Gage dish_trade_payment_db(DB db, string cid) {
            db.SetSQL("INSERT INTO dish_trade_payment(seq, [type], [state], amt, shop, term, operator, balance, uid) VALUES(@seq, @type, @state, @amt, @shop, @term, @oper, @bal, @uid)")
                .ResetParam()
                .AddParam("@amt", DBType.Money, 0)
                .AddParam("@bal", DBType.Money, 0)
                .AddParam("@uid", DBType.String, 255, "")

                .AddParam("@type", DBType.Int, 1)
                .AddParam("@shop", DBType.Int, terminal.shop.id)
                .AddParam("@term", DBType.Int, terminal.id)
                .AddParam("@oper", DBType.String, 20, terminal.operation)
                .AddParam("@state", DBType.Int, 0)
                .AddParam("@seq", DBType.Long, seq);
            payment.ForEach(s => {
                db.SetParams(s.Item2.ToMoney(), s.Item3.ToMoney(), s.Item4, s.Item5.ToInt()).Execute();
            });
            return this;
        }
        private Gage dish_trade_details_db(DB db, DishProduct[] Products) {
            db.SetSQL("INSERT INTO dish_trade_details(seq, type, pid, name, price, amt, cateid, window, uid,`weight`) VALUES(@seq, @type, @pid, @name, @price, @amt, @cate, @window, @uid,@weight)")
                    .ResetParam()
                    .AddParam("@type", DBType.Int, 1)
                    .AddParam("@pid", DBType.String, 255, "")
                    .AddParam("@name", DBType.String, 255, "")
                    .AddParam("@price", DBType.Money, 0)
                    .AddParam("@amt", DBType.Money, 0)
                    .AddParam("@cate", DBType.Int, 0)
                    .AddParam("@window", DBType.Int, 0)
                    .AddParam("@uid", DBType.String, 255, "")
                    .AddParam("@weight", DBType.Int, 0)
                    .AddParam("@seq", DBType.Long, seq);
            // TODO
            //for (int i = 0; i < Products.Length; i++) {
            //    db.SetParams(Products[i].ptype
            //        , Products[i].id
            //        , Products[i].name
            //        , Products[i].price * .01
            //        , Products[i].amt * .01
            //        , Products[i].cate
            //        , Products[i].window
            //        , "" + Products[i].uid
            //        , Products[i].book
            //        ).Execute();
            //}
            return this;
        }
        private Gage dish_trade_db(DB db, Card c, long seq, int qty, ulong amt, ulong dues, int daypart, string[] times, long? balance, string invoice, DateTime? sell_date = null, int offline = 0, string remark = null,int gage = 0) {
            string cid = "";
            string cno = "";
            long cardsn = 0;
            int owner = 0;
            if (c != null) {
                cid = c.id;
                cno = c.no;
                if (payment.Count > 1) {
                    cid = cid.match(@"^\d+");
                    cno = cno.match(@"^\d+");
                    balance = -999999999;
                }
                cardsn = c.baseid;
                owner = c.group_id;
            }
            db.ResetParam()
            .AddParam("@seq", DBType.Long, seq)
            .AddParam("@shop", DBType.Int, terminal.shop.id)
            .AddParam("@term", DBType.Int, terminal.id)
            .AddParam("@owner", owner)
            .AddParam("@operator", DBType.String, 20, terminal.operation)
            .AddParam("@amt", DBType.Money, amt * .01)
            .AddParam("@amt_dues", DBType.Money, dues * .01)
            .AddParam("@qty", DBType.Int, qty)
            .AddParam("@times", DBType.Int, daypart)
            .AddParam("@stime", times[0].ToDateTime())
            .AddParam("@etime", times[1].ToDateTime())
            .AddParam("@card", DBType.String, 20, cid)
            .AddParam("@cardno", DBType.String, 20, cno)
            .AddParam("@cardsn", DBType.Long, cardsn)
            .AddParam("balance", DBType.Money, balance * .01)
            .AddParam("invoice", DBType.Long, invoice)
            .AddParam("@sell_date", DBType.DateTime, sell_date == null || sell_date == DateTime.MinValue ? new DateTime(2000, 1, 1) : sell_date);
            if (remark != null) db.AddParam("remark", DBType.String, 255, remark.Left(255));
            if (c != null) db.AddParam("card_level", c.level.id);
            if (c != null && c.profile != null) db.AddParam("card_profile", c.profile.id);
            if (gage == 1) {
                db.AddParam("properties", "{\"gage\":\"1\"}");
            }
            db.Insert("dish_trade");
            return this;
        }
        public bool Exists(DB db, string invoice) {
            db.ResetParam()
            .AddParam("@term", terminal.id)
            .AddParam("@invoice", DBType.Long, invoice);
            string str = db.ExecuteString("SELECT seq FROM dish_trade  WHERE term = @term AND invoice = @invoice limit 1");
            if (str.IsEmpty()) return false;
            seq = str.ToLong();
            return true;
        }
        private Gage cardmix(DB db, Card c, int[] rank, ulong amt, ulong amt_dues, string invoice, Major major) {
            payment.Clear();
            if (rank.Length == 0) return Error(6);
            string lnkid = "";
            string lnkno = "";
            lnkid = card.param.Get("linkid");
            lnkno = card.param.Get("linkno");
            long bal = long.Parse(card.credit.ToString()); ;  //先加可透支金额
            if (rank.Length == 1) {
                try {
                    if (rank[0] == 0) bal += c.BalanceUsable.ToMoneyX();
                    else {
                        bal += (long)c.Accounts[rank[0]].balance;
                        c.SetAccount(rank[0]);
                    }
                    if (bal < (long)amt) return Error(14);
                    c.SetMajor(major);
                }
                catch {
                    return Error(10);
                }
                c.Pay(db, amt, invoice);
                if (c.GetError(out err)) return Error(err.Code, err.SubCode, err.Message);
                bal -= (long)amt;
                long bal_pay = rank[0] == 0 ? bal - long.Parse(card.credit.ToString()) + long.Parse(card.freeze.ToString()) : bal;
                if (lnkid.IsEmpty()) {
                    payment.Add(Tuple.Create(rank[0], amt, bal_pay, "{0},{1}".format(rank[0] == 0 ? c.id : c.id + ".1", rank[0] == 0 ? c.no : c.no + ".1"), 1));
                }
                else {
                    payment.Add(Tuple.Create(rank[0], amt, bal_pay, "{0},{1}".format(rank[0] == 0 ? lnkid : lnkid + ".1", rank[0] == 0 ? lnkno : lnkno + ".1"), 21));
                }
                return this;
            }
            rank = rank.Where(n => n == 0 || c.Accounts.ContainsKey(n)).ToArray();
            Dictionary<int, long> bals = rank.ToDictionary(n => n, n => {
                if (n == 0) { return bal + c.BalanceUsable.ToMoneyX(); }
                return (long)c.Accounts[n].balance;
            });
            int tick = 0;
            long a;
            long amtbal = (long)amt;
            int rank_count = rank.Length - 1;//余额不足验证
            int count = 0;
            foreach (var i in rank) {
                if (count > 0) c.param.card_lock_pass = true;
                count++;
                c.SetAccount(i);
                if ((!bals.ContainsKey(i) || (bals[i] <= 0 && (count - 1) < rank_count))) continue;
                a = bals[i];
                if ((amt != 0 && a <= 0) || (a < amtbal && count > 1) || (rank.Length == 1 && a < amtbal)) return Error(14);
                a = Math.Min(a, amtbal);
                if (tick > 0 && amtbal < (long)amt) {
                    c.VFY_INVOICE = false;
                }
                c.SetMajor(major);
                if ((amt != 0 && a <= 0)) return Error(14);
                c.Pay(db, (ulong)a, invoice);
                if (c.GetError(out err)) {
                    if (err.Code != 14) { return this; }
                    else {
                        return Error(err.Code);
                        // return Error(14);
                    }
                }
                bals[i] -= a;
                long bal_pay = i == 0 ? bals[i] - long.Parse(card.credit.ToString()) + long.Parse(card.freeze.ToString()) : bals[i];
                if (lnkid.IsEmpty()) {
                    payment.Add(Tuple.Create(i, (ulong)a, bal_pay, "{0},{1}".format(i == 0 ? c.id : c.id + ".1", i == 0 ? c.no : c.no + ".1"), 1));
                }
                else {
                    payment.Add(Tuple.Create(i, (ulong)a, bal_pay, "{0},{1}".format(i == 0 ? lnkid : lnkid + ".1", i == 0 ? lnkno : lnkno + ".1"), 21));
                }
                amtbal -= a;
                if (amtbal == 0) break;
                tick++;
            }
            return this;
        }
        #endregion
        public Gage getProdTable(DB db,out string str) {
            str = "prod";
            if (null == terminal || null == terminal.shop) {
                return Error(3, "term or shop error");
            }
            int shop = terminal.shop.id;
            if (shop < 100) return Error(3, shop + "-shop error less then 100");
            if (db.ExistsTable("prod" + shop)) {
                str = str + shop;
            }
            return this;
        }
        public Gage prod_list(out DBItems list) {
            list = new DBItems();
            using (DB db = new DB(snapshot: true)) {
                string prodname = "prod";
                if(getProdTable(db,out prodname).GetError()) return Error();
                GagePolicy p = new GagePolicy(terminal);
                int policyid = p.CheckFoodPrice(terminal.shop.id, DateTime.Now);
                if (policyid > 0) {
                    string sql = "select `id`,`no`,`name`,`pinyin`,`unit` from  "+ prodname + " where `state`=1 and id in(select DISTINCT id from dish_prices where policy=@policy)";
                    list = db.ResetParam().AddParam("@policy", DBType.Int, policyid).ExecuteItems(sql);
                }
                // list.Total = count;
            }
            return this;
        }
        public Gage prod_get(string pid) {
            using (DB db = new DB(snapshot: true)) {
                string prodname = "prod";
                if (getProdTable(db, out prodname).GetError()) return Error();
                DBItems prod = db.ResetParam().AddParam("id", pid.ToInt()).ExecuteItems("select `id`,`name`,`state`,`remark`,`properties`,`price`,`unit` from  "+ prodname + " where `id`=@id and `type`=111");
                if (prod.Length <= 0) return Error(4, 1, "prod not found");
                if (prod[0]["state"].ToInt() != 1) return Error(4, 2, "prod disabled");

                bool isexist = false;
                #region 取排菜策略-每100g价格price
                GagePolicy p = new(terminal);
                int policyid = p.CheckFoodPrice(terminal.shop.id, DateTime.Now);
                if (policyid > 0) {
                    string sql = @"select  p.`price`,p.`times`
                                from dish_prices p
                                where p.`type` = 2 and p.`state` < 11  and p.policy = @policy and p.id=@pid";
                    DBItems price_db = db.ResetParam().AddParam("@policy", DBType.Int, policyid).AddParam("@pid", DBType.Int, pid).ExecuteItems(sql);
                    if (price_db.Length > 0) {
                        int part = getPart(db);
                        if (part >= 0) {
                            for (int j = 0; j < price_db.Length; j++) {
                                if (price_db[j]["times"].ToInt() == part) {
                                    isexist = true;
                                    param.price = double.Parse(price_db[j]["price"]) * 100.ToInt();
                                    param.part = part;
                                    break;
                                }
                            }
                        }
                    }
                }
                #endregion
                if (!isexist) return Error(4, 1, "prod not found -policy." + policyid);//找不到策略 返回菜品不存在


                param.id = prod[0]["id"];
                param.name = prod[0]["name"];
                param.remark = prod[0]["remark"];
                param.unit = prod[0]["unit"];
                //param.price = double.Parse(prod[0]["price"].ToStr()) * 100;
                string properties_str = prod[0]["properties"];
                var properties = properties_str.ToJDict();
                #region 营养nutrition
                dynamic nutrtion_100 = "";
                double ener_t = 0; double pro_t = 0; double fat_t = 0; double car_t = 0; double na_t = 0;
                string dbexpand = "[{\"item\":\"能量\",\"value\":0, \"unit\":\"kcal\"},{\"item\":\"蛋白质\",\"value\":0, \"unit\":\"g\"},{\"item\":\"脂肪\",\"value\":0, \"unit\":\"g\"},{\"item\":\"碳水化合物\",\"value\":0, \"unit\":\"g\"},{\"item\":\"钠\",\"value\":0, \"unit\":\"mg\"}]";
                dynamic obj = dbexpand.ToJDict();
                if (properties != null) {
                    if (properties.ContainsKey("nutrition_100")) {
                        nutrtion_100 = properties["nutrition_100"];
                        ener_t = nutrtion_100.ContainsKey("energy_kcal") ? double.Parse(nutrtion_100["energy_kcal"]) : 0;
                        pro_t = nutrtion_100.ContainsKey("protein") ? double.Parse(nutrtion_100["protein"]) : 0;
                        fat_t = nutrtion_100.ContainsKey("fat") ? double.Parse(nutrtion_100["fat"]) : 0;
                        car_t = nutrtion_100.ContainsKey("cho") ? double.Parse(nutrtion_100["cho"]) : 0;
                        na_t = nutrtion_100.ContainsKey("na") ? double.Parse(nutrtion_100["na"]) : 0;
                    }
                    else if (properties.ContainsKey("nutrition")) {
                        dynamic nutrtion = properties["nutrition"];
                        double weight = properties.ContainsKey("weight") ? double.Parse(properties["weight"].ToString() == "" ? "100" : properties["weight"].ToString()) : 100;
                        double nutrition_rate = 100 / weight;
                        ener_t = double.Parse(nutrtion["energy_kcal"]) * nutrition_rate;
                        pro_t = double.Parse(nutrtion["protein"]) * nutrition_rate;
                        fat_t = (double.Parse(nutrtion["fat"]) * nutrition_rate);
                        car_t = (double.Parse(nutrtion["cho"]) * nutrition_rate);
                        na_t = (double.Parse(nutrtion["na"]) * nutrition_rate);
                    }
                    else {

                    }
                }
                obj[0]["value"] = ener_t;
                obj[1]["value"] = pro_t;
                obj[2]["value"] = fat_t;
                obj[3]["value"] = car_t;
                obj[4]["value"] = na_t;
                param.nutrition = obj;
                #endregion
                #region 标签tag
                List<tagmode> tag_list = new List<tagmode> { };
                if (properties != null) {
                    string tag = "";
                    string[] tag_arr;
                    if (properties.ContainsKey("labs_prod1")) {
                        tag = properties["labs_prod1"].Tostring();
                        tag_arr = tag.Split(',');
                        foreach(var item in tag_arr) {
                            if (!item.IsEmpty()) {
                                tag_list.Add(new tagmode { tag = 1, name = item });
                            }
                        }
                    }
                    if (properties.ContainsKey("labs_prod2")) {
                        tag = properties["labs_prod2"].Tostring();
                        tag_arr = tag.Split(',');
                        foreach (var item in tag_arr) {
                            if (!item.IsEmpty()) {
                                tag_list.Add(new tagmode { tag = 2, name = item });
                            }
                        }
                    }
                    if (properties.ContainsKey("labs_prod3")) {
                        tag = properties["labs_prod3"].Tostring();
                        tag_arr = tag.Split(',');
                        foreach (var item in tag_arr) {
                            if (!item.IsEmpty()) {
                                tag_list.Add(new tagmode { tag = 3, name = item });
                            }
                        }
                    }
                }
                param.tag = tag_list.JSON().ToJDict();
                #endregion
               
            }
            return this;
        }
        private int getPart(DB db) {
            int part = -1;
            string str = "";
            if (null != terminal && null != terminal.shop && terminal.shop.id != 1) {
                str = db.GetConfig($"shop_hours_{terminal.shop.id}").Trim().Trim('`');
            }
            if (str.IsEmpty()) str = db.GetConfig("shop_hours").Trim().Trim('`');
            if (str.IsEmpty()) return part;
            string shop_hours = str;
            if (!shop_hours.StartsWith("[")) shop_hours = "[" + shop_hours;
            if (!shop_hours.EndsWith("]")) shop_hours = shop_hours + "]";
            if (!shop_hours.IsEmpty()) {
                var times = shop_hours.JSONDecode<dynamic[]>();
                var DayPart = new int[5, 2];
                int[] arr = times.Select((s, i) => {
                    string st = s["etime"];
                    DayPart[i + 1, 1] = st.Replace(":", "").ToInt();
                    st = s["stime"];
                    DayPart[i + 1, 0] = st.Replace(":", "").ToInt();
                    return st.Replace(":", "").ToInt();
                }).ToArray();
                part = Part(DayPart);
            }
            return part;
        }
        private int Part(int[,] ts) {
            int t = DateTime.Now.ToString("HHmm").ToInt();
            int n = 0;
            int br = 0;
            for (int i = 1; i < ts.Length / 2; i++) {
                if (t > ts[i, 0]) n = i;
                if (br == 0 && ts[i, 0] > ts[i, 1]) br = i;
            }
            for (int i = 1; i <= br; i++) {
                if (t >= ts[i, 0]) n = i;
            }
            n = n == 0 ? br : n;
            return n;

        }
        public Gage trade(string seq, string cardid, string rfid_uid, string pid, int price, int takeqty, DateTime taketime, int part =1,string stallid = "") {
            using (DB db = new DB(snapshot: true)) {
                bool exists = db.ResetParam().AddParam("@seq",  seq).AddParam("@shop", terminal.shop.id).AddParam("@term", terminal.id).ExecuteString("select COUNT(1) from gage_trade where seq=@seq and shop=@shop and term=@term limit 1").ToInt() == 1;
                if (exists) return this;
                if (terminal == null || terminal.shop == null) {
                    return Error(3, 0, "term error");
                }
                int card_group = 0;int card_level = 0;
                DBItems card_db = db.AddParam("@cardid", cardid).ExecuteItems("select `group`,`level` from card where id=@cardid limit 1");
                if (card_db.Length > 0) {
                    card_group = card_db[0]["group"].ToInt();
                    card_level= card_db[0]["level"].ToInt();
                }
                string pname = "";
                int pcate = 0;
                string prodname = "prod";
                if (getProdTable(db, out prodname).GetError()) return Error();
                int pidint = pid.ToInt();
                db.AddParam("@pid", pidint);
                if (pidint == 0) {
                    pname = "托盘机定额消费";
                }
                else {
                    DBItems prod = db.ExecuteItems("select name,`cate` from " + prodname + " where id=@pid limit 1");
                    if (prod.Length > 0) {
                        pname = prod[0]["name"];
                        pcate = prod[0]["cate"].ToInt();
                    }
                }
                DateTime now = DateTime.Now;
                string sql = @"insert into gage_trade(seq,shop,term,dish_id,cardid,card_level,card_group,pid,pname,weight,`part`,price,`amt`,taketime,tick,create_date,`cate`,`stall`)
                            values(@seq,@shop,@term,@dishid,@cardid,@card_level,@card_group,@pid,@pname,@weight,@part,@price,@price/100*@weight,@taketime,unix_timestamp(),now(),@pcate,@stallid)";
                db.AddParam("@dishid", rfid_uid)
                     .AddParam("@card_level", card_level)
                     .AddParam("@card_group", card_group)
                     .AddParam("@pcate", pcate)
                     .AddParam("@pname", pname)
                     .AddParam("@weight", takeqty)
                     .AddParam("@part", part)
                     .AddParam("@price", price)
                     .AddParam("@taketime", taketime)
                     .AddParam("@stallid", stallid.ToInt(0))
                .Execute(sql);
                db.Commit();
            }
            return this;
        }
        public Gage BalanceEnough(DB db,string cardid) {
            bool balance_enough = false;
            ErrorInfo e;
            if (GetCardByUID(db, cardid, "").GetError(out e)) return Error(4, 1, "invalid card." + e.Code + "." + e.SubCode);
            long balance = 0;
            balance += long.Parse(card.credit.ToString()); ;  //先加可透支金额
                                                              //取消费规则 new int[]{ 0,1 };
            var account_rank = ((string)terminal.properties.GetValue("account_rank", "")).Split(",").Select(s => s.ToInt(-5)).ToArray();
            //消费规则有主账户
            if (account_rank.Contains(0)) {
                balance += card.BalanceUsable.ToMoneyX();//BalanceUsable 已经算好了,= 余额-冻结-已透支
            }
            if (account_rank.Contains(1)) {
                if (card.Accounts != null) { //补贴账户
                    foreach (var ac in card.Accounts.Values) {
                        balance += long.Parse(ac.balance.ToString());
                    }
                }
            }
            int levelid = card.level.id;
            int shopid = terminal.shop.id;
            DBItems db_policy = db.ResetParam().ExecuteItems("select id,type,name,`trigger`,`action`,`state`,remark,create_date,last_date from policy where type = 42 and state = 1");
            if (db_policy.Length <= 0) {
                return Error(4, 14, "未找到策略");
            }
            bool policy_exist = false;
            string trig = string.Empty;
            string action = string.Empty;
            string shop = string.Empty;
            string lv = string.Empty;
            for (int i = 0; i < db_policy.Length; i++) {
                trig = db_policy[i]["trigger"];
                action = db_policy[i]["action"];
                var jsont = trig.ToJDict();
                var jsona = action.ToJDict();
                lv = "0";
                if (jsont.ContainsKey("1")) {
                    lv = jsont["1"].ToString();
                    if (lv.IsEmpty()) lv = "0";
                }
                shop = "0";
                if (jsont.ContainsKey("11")) {
                    shop = jsont["11"].ToString();
                    if (shop.IsEmpty()) shop = "0";
                }
                //由于保存策略的时候已经做了冲突判断，故此处取出符合条件的策略即当成当前应用的策略
                if ((shop == "0" || shop.Split(',').Contains(shopid.ToString())) && (lv == "0" || lv.Split(',').Contains(levelid.ToString()))) {
                    policy_exist = true;
                    int mode = 0;
                    if (jsona.ContainsKey("11")) {
                        mode = int.Parse(jsona["11"].ToString());
                    }
                    if (mode == 0 || mode == 3) {//结算台模式，免费模式，无需判断余额
                        balance_enough = true;
                    }
                    else if (mode == 1 && balance > 0) {//计量结算模式 余额大于0
                        balance_enough = true;
                    }
                    else if ((mode == 2|| mode == 4) && jsona.ContainsKey("3") && balance >= long.Parse(jsona["3"].ToString())) {//定额模式 余额大于等于定额
                        balance_enough = true;
                    }
                    break;
                }
            }
            if (!policy_exist) return Error(4, 14, "未找到策略");
            if (!balance_enough) return Error(4, 14, "余额不足");
            return this;
        }
        public Gage empl_get(string cardid) {
            using (DB db = new DB(snapshot: true)) {
                DBItems db_profile = db.ResetParam().AddParam("cardid", cardid).ExecuteItems("select c.`state`,c.`locked`,c.expire_date, p.id,p.`mob` no,p.`name`,p.gender,p.properties from  card c, card_profile p where c.`profile`=p.id and c.`id`=@cardid limit 1");
                if (db_profile.Length <= 0) {
                    return Error(4, 1, "人员未授权");
                }
                int c_state = db_profile[0]["state"].ToInt();
                if (c_state != 1) {
                    if (c_state == 3) {
                        return Error(4, 11, "卡已挂失");
                    }
                    return Error(4, 2, "卡状态异常");
                }
              
                if (db_profile[0]["locked"].ToInt() == 1) return Error(4, 13, "卡被锁定");
                if (db_profile[0]["expire_date"].ToDateTime() < DateTime.Now) {
                    return Error(4, 12, "卡已过期");
                }
                #region 算余额够不够
                if (BalanceEnough(db, cardid).GetError(out err)) return Error(err.Code, err.SubCode, err.Message);
                #endregion
                string id = db_profile[0]["id"];
                string no = db_profile[0]["no"];
                string name = db_profile[0]["name"];
                string sex = db_profile[0]["gender"].ToInt() == 1 ? "男" : "女";
                double weight = 0;
                double height = 0;
                string properties = db_profile[0]["properties"];
                double bmi = 0;
                if (!properties.IsEmpty()) {
                    var prope = properties.ToJDict();
                    if (prope.ContainsKey("weight") && !prope["weight"].Tostring().IsEmpty()) {
                        weight = double.Parse(prope["weight"].ToString());
                    }
                    if (prope.ContainsKey("height") && !prope["height"].Tostring().IsEmpty()) {
                        height = double.Parse(prope["height"].ToString());
                    }
                    if (height != 0) {
                        bmi = Math.Round(weight / ((height / 100) * (height / 100)), 1);
                    }
                }
                //string emp_str = "{\"id\":\"{0}\",\"no\":\"{1}\",\"name\":\"{2}\",\"sex\":\"{3}\",\"weight\":{4},\"height\":{5},\"bmi\":{6} }".format(id, no, name, sex, weight, height, bmi);
                string emp_str = "{\"id\":\"" + id + "\",\"no\":\"" + no + "\",\"name\":\"" + name + "\",\"sex\":\"" + sex + "\",\"weight\":" + weight + ",\"height\":" + height + ",\"bmi\":" + bmi + " }";
                param.employee = emp_str.ToJDict();
            }
            return this;
        }
        public Gage empl_remind(string cardid,string prodid) {
            using (DB db = new DB(snapshot: true)) {
                string prodname = "prod";
                if (getProdTable(db, out prodname).GetError()) return Error();
                #region 建议取用
                param.suggest = 1;//默认为适量食用
                DBItems db_prod = db.ResetParam().AddParam("prodid", prodid.ToInt()).ExecuteItems("select labs_healthcard from  "+ prodname + " where `state`=1 and id=@prodid limit 1");
                if (db_prod.Length > 0) {
                    string[] labs_healthcard = db_prod[0]["labs_healthcard"].Split(",");
                    if (labs_healthcard.Length > 0) {
                        if (!labs_healthcard[0].IsEmpty()) {
                            param.suggest = healthcard_dic_suggest[labs_healthcard[0]];
                        }
                    }
                }
                #endregion
                if (!cardid.IsEmpty()) {
                    DBItems db_profile = db.ResetParam().AddParam("cardid", cardid).ExecuteItems("select c.`state`, p.id,p.`mob` no,p.`name`,p.gender,case when p.birthday<='1900-01-01' then 18 else TIMESTAMPDIFF(YEAR, p.birthday, CURDATE()) end age ,p.properties from  card c, card_profile p where c.`profile`=p.id and c.`id`=@cardid limit 1");
                    if (db_profile.Length <= 0) {
                        return Error(4, 1, "人员未授权");
                    }
                    if (db_profile[0]["state"].ToInt() != 1) return Error(4, 2, "人员被停用");
                    string properties = db_profile[0]["properties"];
                    int part = getPart(db);
                    double rate = 0.3;
                    switch (part) {
                        case 2:
                            rate = 0.4;
                            break;
                        default:
                            rate = 0.3;
                            break;
                    }
                  
                    #region 弃用
                    //int healthcard = 1;// 1、2、3 分别表示 绿、黄、红牌
                    //string healthcard_str = "绿牌";
                    //if (prope.ContainsKey("healthcard")) {
                    //    healthcard = int.Parse(prope["healthcard"].ToString());
                    //    if (healthcard_dic.ContainsKey(healthcard)) {
                    //        healthcard_str = healthcard_dic[healthcard];
                    //    }
                    //}
                    //DBItems db_prod = db.ResetParam().AddParam("prodid", prodid.ToInt()).ExecuteItems("select labs_healthcard from  prod where `state`=1 and id=@prodid limit 1");
                    ////如果菜品未设置红黄绿,则为适量食用
                    //if (db_prod.Length > 0) {
                    //    string[] labs_healthcard = db_prod[0]["labs_healthcard"].Split(",");
                    //    //如果一对一匹配,则为推荐食用
                    //    if(labs_healthcard.Length==1 && labs_healthcard.Contains(healthcard_str)) {
                    //        param.suggest = healthcard_dic_suggest["绿牌"];
                    //    }
                    //    //如果不匹配,则为谨慎食用
                    //    else if (!labs_healthcard.Contains(healthcard_str)) {
                    //        param.suggest = healthcard_dic_suggest["红牌"];
                    //    }
                    //    //其他（匹配但不是一对一）,则为适量食用
                    //    else {
                    //        param.suggest = healthcard_dic_suggest["黄牌"]; 
                    //    }
                    //}
                    #endregion
                    #region 能量建议取用量
                    int suggest_energy = 2250;
                    if (!properties.IsEmpty()) {
                        var prope = properties.ToJDict();
                        int workload = 1;//1、2、3 分别表示 轻体力 中体力 重体力
                        if (prope.ContainsKey("workload")) {
                            if (prope["workload"].ToString() != "") {
                                workload = int.Parse(prope["workload"].ToString());
                            }
                        }
                        int sex = 1 - db_profile[0]["gender"].ToInt();
                        int age = db_profile[0]["age"].ToInt();
                        suggest_energy = db.ResetParam().AddParam("workload", workload)
                            .AddParam("sex", sex)
                            .AddParam("age", age)
                            .ExecuteString("select energy from  gage_eer where workload=@workload and sex=@sex and age<=@age order by age desc limit 1").ToInt(2250);
                    }
                    param.suggest_energy = suggest_energy;
                    param.suggest_energy_min = Convert.ToInt32(suggest_energy * 0.9 * rate);
                    param.suggest_energy_max = Convert.ToInt32(suggest_energy * 1.1 * rate);
                    #endregion
                }
            }
            return this;
        }
        Dictionary<int, string> healthcard_dic = new Dictionary<int, string> { { 1, "绿牌" }, { 2, "黄牌" }, { 3, "红牌" } };
        //0 推荐食用 1 适量食用 2 谨慎食用
        Dictionary<string, int> healthcard_dic_suggest = new Dictionary<string, int> { { "绿牌", 0 }, { "黄牌", 1 }, { "红牌", 2 } };
        public Gage policy_list(out DBItems list, int pi = 0, int ps = 0) {
            using (DB db = new DB(snapshot: true)) {
                string sql = "select `id`,`name`,`remark`,`state` from  policy where `type`=42";
                param.count = db.ExecuteString("select count(1) from  policy where `type`=42").ToInt();
                ps = ps == 0 ? 1000000 : ps;
                sql += " order by `state`,id limit " + (pi * ps) + "," + ps;
                list = db.ExecuteItems(sql);
            }
            return this;
        }
        public Gage policy_get(int policyid) {
            using (DB db = new DB(snapshot: true)) {
                DBItems db_trig = db.ResetParam().AddParam("policyid", policyid).ExecuteItems("select `id`,`name`,`remark`, `trigger`,`action` from policy where `type`=42  and `id`=@policyid limit 1");
                if (db_trig.Length <= 0) {
                    return Error(4, 0, "not found");
                }
                string trig_str = db_trig[0]["trigger"];
                var trig = trig_str.ToJDict();
                int p_id = db_trig[0]["id"].ToInt();
                string p_name = db_trig[0]["name"].ToString();
                string p_remark = db_trig[0]["remark"].ToString();
                string p_action = db_trig[0]["action"].ToString();
                var action = p_action.ToJDict();
                string p_mode = "0";
                if (action.ContainsKey("11")) {
                    p_mode = (action["11"] + "");
                }
                string p_time = "0";
                if (action.ContainsKey("6")) {
                    p_time = (action["6"] + "");
                }
                if (p_time != "0") {
                    p_time = int.Parse(p_time) / 60 + "";
                }
                string p_amt = "0";
                if (action.ContainsKey("3")) {
                    p_amt = (action["3"] + "");
                }

                string passtime = "-1";
                if(action.ContainsKey("5")) {
                    passtime = (action["5"] + "");
                }
                string policy_mode = "{\"id\":" + p_id + ",\"name\":\"" + p_name + "\",\"remark\":\"" + p_remark + "\",\"mode\":" + p_mode + ",\"time\":" + p_time + ",\"amt\":" + p_amt + "}";
                if(p_mode == "1" && passtime.ToInt(-1) >= 0) {
                    passtime = int.Parse(passtime) / 60 + "";
                    policy_mode = "{\"id\":" + p_id + ",\"name\":\"" + p_name + "\",\"remark\":\"" + p_remark + "\",\"mode\":" + p_mode + ",\"passtime\":" + passtime + ",\"time\":" + p_time + ",\"amt\":" + p_amt + "}";
                }
               
                param.policy= policy_mode.ToJDict();
                string shopid = "0";
                string cardtype = "0";
                if (trig.ContainsKey("11")) {
                   shopid = trig["11"].ToString();
                   param.shop = db.ExecuteItems("select `id`,`name` from shop where `id` in({0})".format(shopid)).Extend();
                }
                else {
                   param.shop = "[{ \"id\":\"0\",\"name\":\"全部餐厅\"}]".ToJDict(); 
                }
                if (trig.ContainsKey("1")) {
                    cardtype = trig["1"].ToString();
                    param.cardtype = db.ResetParam().AddParam("cardtypeid", cardtype.Replace(",", "-")).ExecuteItems("select `id`,`name` from card_level where  FIND_IN_SET(`id`,REPLACE(@cardtypeid,'-',','))  ").Extend();
                }
                else {
                    param.cardtype = "[{ \"id\":\"0\",\"name\":\"全部卡类型\"}]".ToJDict();
                }
            }
            return this;
        }
        /// <summary>
        /// 策略更新/新增
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="remark"></param>
        /// <param name="shopid"></param>
        /// <param name="cardtype"></param>
        /// <param name="mode"></param>
        /// <param name="time"></param>
        /// <param name="amt"></param>
        /// <returns></returns>
        public Gage policy_update(int id, string name,string shopid, string cardtype, int mode, string remark, int time,int amt, int passtime) {
            DBItems db_trig = new DBItems();
            DBItems db_trig_exist = new DBItems();
            using (DB db = new DB(snapshot: true)) {
                if (cardtype.Split(',').Contains("undefined")) {
                    return Error(3, 0, "卡类型参数错误");
                }
                if (shopid.Split(',').Contains("undefined")) {
                    return Error(3, 0, "餐厅参数错误");
                }
                string trigger= "{";
                if (cardtype != "0") {
                    trigger += "1:\"" + cardtype + "\",";
                }
                if (shopid != "0") {
                    trigger += "11:\"" + shopid + "\",";
                }
                if (trigger.Right(1) == ",") trigger = trigger.Left(trigger.Length - 1);
                trigger += "}";
                if (mode == 0) time = 0;
                if (mode != 2 && mode != 4 && mode != 5) amt = 0;
                string action = "{11:\"" + mode + "\",6:\"" + (time * 60) + "\",3:\"" + amt + "\"}";

                if(mode == 1 && passtime >= 0) {
                    action = "{11:\"" + mode + "\",5:\"" + (passtime * 60) + "\",6:\"" + (time * 60) + "\",3:\"" + amt + "\"}";
                }
                db.ResetParam().AddParam("policyid", id);
                string sql_list = "select `trigger` from policy where `type`=42 and  `state`=1";
                string sql_update = "";
                //新增
                if (id == 0) {
                    db_trig = db.ExecuteItems(sql_list);
                    if (policy_repeated(db_trig,shopid, cardtype)) return Error(300, 0, "与现有策略冲突，不允许操作");
                    db.AddParam("name", name).AddParam("trigger", trigger).AddParam("action", action).AddParam("remark", remark);
                    sql_update = "insert into policy(`type`,`name`,`trigger`,`action`,`state`,`remark`) values(42,@name,@trigger,@action,1,@remark)";
                    db.Execute(sql_update);
                }
                else {
                    db_trig_exist = db.ExecuteItems("select `trigger` from policy where `id`=@policyid limit 1");
                    if (db_trig_exist.Length <= 0) {
                        return Error(4, 0, "not found");
                    }
                    db_trig = db.ExecuteItems(sql_list+ " and `id`!=@policyid");
                    if (policy_repeated(db_trig, shopid, cardtype)) return Error(300, 0, "与现有策略冲突，不允许操作");
                    db.AddParam("name", name).AddParam("trigger", trigger).AddParam("action", action).AddParam("remark", remark);
                    sql_update = "update policy set `name`=@name,`trigger`=@trigger,`action`=@action,`remark`=@remark where `id`=@policyid";
                    db.Execute(sql_update);
                }
                db.Commit();
            }
            RelateRemove(db_trig_exist);
            return this;
        }
        public bool policy_repeated(DBItems db_trig,string shopid,string cardtype) {
            bool flag = false;
            for (int i = 0; i < db_trig.Length; i++) {
                string trig_str = db_trig[i]["trigger"];
                var trig = trig_str.ToJDict();
                if (policy_repeated("11", trig, shopid) && policy_repeated("1", trig, cardtype)) {
                    flag = true;
                    break;
                }
            }
            return flag;
        }
        public bool policy_repeated(string type, dynamic trig,string str) {
            bool flag = false;
            if (!trig.ContainsKey(type) || str == "0") {
                flag = true;
            }
            else {
                string[] list_str = (trig[type].ToString()).Split(',');
                List<string> inter_str = list_str.Intersect(str.Split(',')).ToList();//交集
                if (inter_str.Count > 0) {
                    flag = true;
                }
            }
            return flag;
        }
      
        public Gage policy_state(int policyid, int state) {
            DBItems db_trig = new DBItems();
            using (DB db = new DB(snapshot: true)) {
                db_trig = db.ResetParam().AddParam("policyid", policyid).ExecuteItems("select `trigger` from policy where `type`=42  and `id`=@policyid");
                if (db_trig.Length <= 0) {
                    return Error(4, 0, "not found");
                }
                if (state == 1) {
                    string trig_str = db_trig[0]["trigger"];
                    var trig = trig_str.ToJDict();
                    string shopid = "0";
                    string cardtype = "0";
                    if (trig.ContainsKey("11")) {
                        shopid = trig["11"].ToString();
                    }
                    if (trig.ContainsKey("1")) {
                        cardtype = trig["1"].ToString();
                    }
                    DBItems db_triglist = db.ExecuteItems("select `trigger` from policy where `type`=42 and  `state`=1 and `id`!=@policyid");
                    if (policy_repeated(db_triglist, shopid, cardtype)) return Error(300, 0, "与现有策略冲突，不允许操作");
                }

                db.AddParam("state", state).Execute("update policy set `state`=@state where `id`=@policyid");
                db.Commit();
            }
            RelateRemove(db_trig);
            return this;
        }
        public void  RelateRemove(DBItems ds) {
            if (ds.Length <= 0) return;
            Redis.Using(rd => {
                #region 移除
                Dictionary<string, string> jsont = ((ds[0]["trigger"]).JSONDecode<Dictionary<string, object>>()).ToDictionary(d => d.Key, d => d.Value.ToString());
                if (!jsont.ContainsKey("11")) {
                    rd.Remove("profile_tray_relate:autotime:0");
                }
                else {
                    jsont["11"].Split(',').Where(s => !s.IsEmpty()).ToList().ForEach(s => {
                        rd.Remove($"profile_tray_relate:autotime:{s}");
                        if (!jsont.ContainsKey("1") || jsont["1"] == "0") {
                            rd.Remove($"gage_policy:action:{s}:0");
                            rd.Remove($"profile_tray_relate:autotime:{s}:0");
                        }
                        else {
                            jsont["1"].Split(',').Where(t => !t.IsEmpty()).ToList().ForEach(t => {
                                rd.Remove($"gage_policy:action:{s}:{t}");
                                rd.Remove($"profile_tray_relate:autotime:{s}:{t}");
                            });
                        }
                    });
                }
                #endregion
            });
        }
        public Gage policy_delete(int policyid) {
            DBItems ds = new DBItems();
            using (DB db = new DB()) {
                ds = db.ResetParam().AddParam("policyid", policyid).ExecuteItems("select * from policy where `id`=@policyid and `type`=42");
                if (ds.Length <= 0) {
                    return Error(4, 0, "not found");
                }
                db.Execute("delete from `policy` where `id`=@policyid and `type`=42");
                db.Commit();
            }
            RelateRemove(ds);
            return this;
        }
        public Gage trade_fail_list(out DBItems list,DateTime starttime, DateTime endtime,int shopid = 0, int pi = 0,int ps = 15) {
            list = new DBItems();
            using (DB db = new DB(snapshot: true)) {
                db.ResetParam().AddParam("@starttime", DBType.DateTime, starttime)
                    .AddParam("@endtime", DBType.DateTime, endtime)
                    .AddParam("@pi", DBType.Int, pi* ps)
                    .AddParam("@ps", DBType.Int, ps);
                string[] sqls = new string[] {
                    "select f.shop,s.name shopname, f.term,f.cardid,ifnull(l.name,'') cardtypename, f.pid,f.pname,f.weight,f.part,f.price,f.taketime,f.create_date From gage_trade_fail f INNER JOIN shop s on f.shop=s.`id`  left JOIN  card_level l on f.card_level=l.id ",
                    " WHERE  f.taketime>=@starttime and f.taketime<=@endtime ",
                    " ORDER BY f.tick desc ",
                    " limit @pi,@ps "
                };
                if (shopid != 0) {
                    sqls[1] += " and f.`shop`=@shopid";
                    db.AddParam("@shopid", DBType.Int, shopid);
                }
                list = db.ExecuteItems(sqls.Join(" "));
                param.count = db.ExecuteString("select count(1) from gage_trade_fail f " + sqls[1]).ToInt();
            }
            return this;
        }
        public string uc_rd_key = "unconscious:uid:";
        public Gage dish_get(string uid, out dynamic list) {
            DBItems listdb = new DBItems();
            int unconscious_table = 0;
            using (DB db = new DB()) {
                listdb = db.ResetParam().AddParam("uid", uid).ExecuteItems(@"select d.autoid seq,d.pid id,d.`name` name,cast(d.amt*100 as signed) amt,d.weight from dish_trade_pending p, dish_trade_pending_details d 
                            where p.seq = d.seq and p.type = 6 and p.state = 0 and p.uid = @uid");
                unconscious_table = terminal.shop.properties.GetInt("unconscious_table", -1);
                if (unconscious_table == -1) {
                    unconscious_table = db.GetConfig("unconscious_table").ToInt();
                }
               
            }
            list = listdb.Extend();
            if (listdb.Length <= 0 && unconscious_table == 1) {//如果支持无感出品模式
                List<gage_dish> li = new List<gage_dish> { };
                Redis.Using(rd => {
                    string key = uc_rd_key + uid;
                    string val = rd.Get(key);
                    if (!val.IsEmpty()) {
                        unconscious_dish mode = val.ToJObject<unconscious_dish>();
                        li = new List<gage_dish> { new gage_dish() { seq = uid, id = mode.pid, name = mode.pname, amt = mode.amt , weight = 1} };
                    }
                });
                list = li;
            }
            return this;
        }
        public Gage dish_archiv(string uid, string seq_list, string invoice) {
            using (DB db = new DB()) {
                long major = 0;
                if (null != terminal) {
                    major = db.ResetParam().AddParam("invoice", invoice).AddParam("term", terminal.id).ExecuteString("select seq from dish_trade where invoice=@invoice and term=@term limit 1 ").ToLong(0);
                }
                //string[] sql_arr = seq_list.Split('|');
                //for (int i = 0; i < sql_arr.Length; i++) {
                //    db.ResetParam().AddParam("uid", uid).AddParam("seq", sql_arr[i]).AddParam("major", major)
                //        .Execute(@"insert into gage_trade_archiv(seq,shop,term,dish_id,cardid,card_level,card_group,pid,pname,weight,`part`,price,`amt`,taketime,tick,create_date,`mode`,finish_date,`major`,`cate`,`stall`)
                //            select  seq, shop, term, dish_id, cardid, card_level, card_group, pid, pname, weight,`part`, price,`amt`, taketime, tick, create_date, 0, NOW(), @major,`cate`,`stall` from gage_trade
                //            where dish_id = @uid and seq = @seq;delete from gage_trade  where dish_id = @uid and seq = @seq");
                //}
                //与称重自助项目合并 by rl 2019-07-29
                #region
                long seq_pending = 0;
                string cid = string.Empty;
                string cno = string.Empty;
                int profileid = 0;
                int c_level = 0;
                int unconscious_table = terminal.shop.properties.GetInt("unconscious_table", -1);
                if (unconscious_table == -1) {
                    unconscious_table = db.GetConfig("unconscious_table").ToInt();
                }
                string sqltype = "=6";
                if (unconscious_table == 1) sqltype = "in(6,7)";
                DBItems ds_pending = db.ResetParam().AddParam("uid", uid).ExecuteItems("select seq,cardid,cardno,profileid,card_level from dish_trade_pending  where type "+ sqltype + " and state=0 and uid=@uid limit 1");
                if (ds_pending.Length > 0) {
                    seq_pending = ds_pending[0]["seq"].ToLong();
                    cid = ds_pending[0]["cardid"];
                    cno = ds_pending[0]["cardno"];
                    profileid = ds_pending[0]["profileid"].ToInt();
                    c_level = ds_pending[0]["card_level"].ToInt();
                }
                if (seq_pending > 0) {
                    db.ResetParam().AddParam("seq_pending", seq_pending).Execute("update dish_trade_pending set state=1 where seq=@seq_pending limit 1");
                    if (major > 0) {
                        db.ResetParam().AddParam("major", major).AddParam("seq_pending", seq_pending);
                        if (cid.Length == 18 || (c_level != 0 && c_level == db.GetConfig("guest_cardlevel").ToInt())) {//虚拟卡数据 只能是微信支付宝支付 更新dish_trade数据
                            db.AddParam("cid", cid).AddParam("cno", cno).AddParam("profileid", profileid).AddParam("c_level", c_level)
                             .Execute("update dish_trade set  card=@cid,cardno=@cno,cardsn=@cid,card_profile=@profileid,card_level=@c_level where seq=@major limit 1");
                        }
                        if (db.ResetParam().AddParam("invoice", $"{seq_pending},dish_trade_pending").ExecuteExists("select 1 from profile_face_logs where invoice=@invoice limit 1")) {
                            db.AddParam("newseq", $"{major},dish_trade").Execute("update profile_face_logs set invoice=@newseq where invoice=@invoice");
                            db.Execute("update profile_face_logs set state=-1 where invoice=@invoice and state=0");
                        }
                    }
                }
                //bool hasuid = false;
                //if (db.ExecuteExists($"select 1 from dish_trade_pending where type=6 and state=0 and profileid={profileid}")) {
                //    hasuid = true;
                //}
                Redis.Using(rd => {
                    rd.Use(1).Remove($"profile_tray_relate:uid:{uid}");
                    if(unconscious_table == 1) rd.Remove(uc_rd_key + uid);
                });
                #endregion
                db.Commit();
            }
            return this;
        }
        public Gage stall_list(out DBItems list) {
            list = new DBItems();
            using (DB db = new DB(snapshot: true)) {
                if (null != terminal && null != terminal.shop) {
                    int shop = terminal.shop.id;
                    if (shop >= 100) {
                        string sql = "select `id`,shopno `no`,`name` from shop where type=21 and state=1 and parent=@shop";
                        list = db.ResetParam().AddParam("shop", shop).ExecuteItems(sql);
                    }
                }
            }
            return this;
        }
        public Gage pricepolicy_update(int id, string remark, int type, string trigger, string prod, int atype) {
            using (DB db = new DB(snapshot: true)) {
                db.ResetParam()
                    .AddParam("policyid", id).AddParam("type", type).AddParam("remark", remark)
                    .AddParam("trigger", trigger);
                DBItems db_trig = new DBItems();
                string sql_exists = "select `trigger`,`remark` from policy where `type`=@type  and `remark`=@remark";
                string sql_list = "select `trigger` from policy where `type`=@type and  `state`=1";
                string sql_update = "";
                //新增
                if (id == 0 || atype == 1) {
                    db_trig = db.ExecuteItems(sql_exists);
                    if (db_trig.Length > 0) {
                        return Error(3, 1, "策略名称已存在");
                    }
                    if (atype == 1 && id == 0) {
                        return Error(3, 0, "参数错误");
                    }
                    if (policy_repeated41(db.ExecuteItems(sql_list), trigger)) return Error(300, 0, "与现有策略冲突，不允许操作");
                    sql_update = "insert into policy(`type`,`name`,`trigger`,`action`,`state`,`remark`) values(@type,'',@trigger,'{}',1,@remark);SELECT @@Identity";
                    int newpolicyid = db.ExecuteString(sql_update).ToInt();
                    if (atype == 1) {
                        db.AddParam("newpolicyid", newpolicyid);
                        db.Execute("insert into dish_prices(policy, id, times, price, state, windowid, type, dish, qty_book, `rank`,`create_date`) select @newpolicyid,id, times, price, state, windowid, type, dish, qty_book, `rank`,now() from dish_prices where policy=@policyid");
                    }else {
                        if (dish_prices(newpolicyid, prod, db).GetError()) return Error();
                    }
                    param.id = newpolicyid;
                }
                else {
                    int pstate = db.ExecuteString("select `state` from policy where `id`=@policyid and type=@type limit 1").ToInt(0);
                    if (pstate == 0) {
                        return Error(4, 0, "未找到策略");
                    }
                    db_trig = db.ExecuteItems(sql_exists + " and `id`!=@policyid");
                    if (db_trig.Length > 0) {
                        return Error(3, 1, "策略名称已存在");
                    }
                    if (pstate == 1) {
                        if (policy_repeated41(db.ExecuteItems(sql_list + " and `id`!=@policyid"), trigger)) return Error(300, 0, "与现有策略冲突，不允许操作");
                    }
                    sql_update = "update policy set `trigger`=@trigger,`remark`=@remark where `id`=@policyid;";
                    db.Execute(sql_update);
                    //if (dish_prices(id, prod, db).GetError()) return Error();
                    param.id = id;
                }
                db.Commit();
            }
            return this;
        }
        public Gage pricepolicy_update_prod(int id,  string prod) {
            using (DB db = new DB(snapshot: true)) {
                if (id <= 0) {
                    return Error(3, 0, "policyid  error");
                }
                db.ResetParam().AddParam("policyid", id);
                int policyid = db.ExecuteString("select `id` from policy where `id`=@policyid and type=41 limit 1").ToInt(0);
                if (policyid == 0) {
                    return Error(4, 0, "未找到策略");
                }
                if (!prod.StartsWith("[")) prod = "[" + prod;
                if (!prod.EndsWith("]")) prod = prod + "]";
                try {
                    List<dish_prices_timer> prodjson = prod.JSONDecode<List<dish_prices_timer>>();
                    dish_prices_timer mode = prodjson[0];
                    int pid = mode.id;
                    db.ResetParam().AddParam("policyid", policyid).AddParam("pid", pid).Execute("delete from dish_prices where policy=@policyid and `id`=@pid");
                    if (mode.timer1 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 1, mode.timer1_price));
                    }
                    if (mode.timer2 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 2, mode.timer2_price));
                    }
                    if (mode.timer3 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 3, mode.timer3_price));
                    }
                    if (mode.timer4 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 4, mode.timer4_price));
                    }
                   
                }
                catch (Exception ex) {
                    return Error(3, 0, "prod参数错误:" + ex.Message);
                }
                db.Commit();
            }
            return this;
        }
        protected Gage dish_prices(int policyid, string prod, DB db) {
            if (!prod.StartsWith("[")) prod = "[" + prod;
            if (!prod.EndsWith("]")) prod = prod + "]";
            try {
                db.ResetParam().AddParam("policyid", policyid).Execute("delete from dish_prices where policy=@policyid");
                List<dish_prices_timer> prodjson = prod.JSONDecode<List<dish_prices_timer>>();
                foreach (dish_prices_timer mode in prodjson) {
                    int pid = mode.id;
                    if (mode.timer1 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 1, mode.timer1_price));
                    }
                    if (mode.timer2 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 2, mode.timer2_price));
                    }
                    if (mode.timer3 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 3, mode.timer3_price));
                    }
                    if (mode.timer4 == 1) {
                        db.Execute("insert into dish_prices(policy, id, times, price,dish,`type`) values({0},{1},{2},{3},'',2)".format(policyid, pid, 4, mode.timer4_price));
                    }
                }
            }
            catch (Exception ex) {
                return Error(3, 0, "prod参数错误:" + ex.Message);
            }
            return this;
        }
        public bool policy_repeated41(DBItems db_trig, string trigger) {
            #region 
            string shopid = "0";
            string time = "0";
            string week = "0";
            var trig_json = trigger.ToJDict();
            if (trig_json.ContainsKey("11")) {
                shopid = trig_json["11"].ToString();
            }
            if (trig_json.ContainsKey("3")) {
                time = trig_json["3"].ToString();
            }
            if (trig_json.ContainsKey("7")) {
                week = trig_json["7"].ToString();
            }
            if (shopid.IsEmpty()) shopid = "0";
            if (time.IsEmpty()) time = "0";
            if (week.IsEmpty()) week = "0";
            #endregion
            bool flag = false;
            for (int i = 0; i < db_trig.Length; i++) {
                string trig_str = db_trig[i]["trigger"];
                var trig = trig_str.ToJDict();
                #region 餐厅是否有交集
                bool shopconf = false;
                if (!trig.ContainsKey("11") || shopid == "0") {
                    shopconf = true;
                }else {
                    string[] list_str = (trig["11"].ToString()).Split(',');
                    List<string> inter_str = list_str.Intersect(shopid.Split(',')).ToList();//交集
                    if (inter_str.Count > 0) {
                        shopconf = true;
                    }
                }
                if (!shopconf) continue;
                #endregion
                #region 星期是否有交集
                bool weekconf = false;
                if (!trig.ContainsKey("7") || week == "0") {
                    weekconf = true;
                }
                else {
                    string[] list_str = (trig["7"].ToString()).Split(',');
                    List<string> inter_str = list_str.Intersect(week.Split(',')).ToList();//交集
                    if (inter_str.Count > 0) {
                        weekconf = true;
                    }
                }
                if (!weekconf) continue;
                #endregion
                #region 时间是否有交集
                bool timeconf = false;
                if (!trig.ContainsKey("3") || time == "0") {
                    timeconf = true;
                }
                else {
                    string stime_e = (trig["3"].ToString()).Split(',')[0];
                    string etime_e = (trig["3"].ToString()).Split(',')[1];
                    string stime = time.Split(',')[0];
                    string etime = time.Split(',')[1];
                    if (!(stime.ToDateTime() >= etime_e.ToDateTime()|| etime.ToDateTime() <= stime_e.ToDateTime())) {
                        timeconf = true;
                    }
                }
                if (!timeconf) continue;
                flag = true;
                break;
                #endregion
            }
            return flag;
        }

        /// <summary>
        /// 补菜/取消补菜
        /// </summary>
        /// <param name="state">1 补菜  0 取消补菜</param>
        /// <returns></returns>
        public Gage Prod_Supply(string state,string weight_remain) {
            //DBItems ds = new DBItems();
            DateTime now = DateTime.Now;
            DB.Using(db => {
                string term_pro = db.ResetParam().AddParam("@termid", terminal.id).ExecuteString("select properties from terminal where id=@termid limit 1");
                Dictionary<string, object> dic_term = new Dictionary<string, object> { };
                string time = "2000-01-01 00:00:00";
                if (!term_pro.IsEmpty()) {
                    dic_term = term_pro.ToJDict();
                    if (!state.IsEmpty()) {
                        time = state.ToInt() == 0 ? "2000-01-01 00:00:00" : now.ToFloor();
                        if (dic_term.ContainsKey("supply")) {
                            dic_term["supply"] = state.ToInt();
                        }
                        else {
                            dic_term.Add("supply", state.ToInt());
                        }
                        if (dic_term.ContainsKey("supply_time")) {
                            dic_term["supply_time"] = time;
                        }
                        else {
                            dic_term.Add("supply_time", time);
                        }
                    }
                    if (!weight_remain.IsEmpty()) {
                        if (dic_term.ContainsKey("weight_remain")) {
                            dic_term["weight_remain"] = weight_remain.ToInt();
                        }
                        else {
                            dic_term.Add("weight_remain", weight_remain.ToInt());
                        }
                    }
                }
                else {
                    if (!state.IsEmpty()) {
                        dic_term.Add("supply", state);
                        dic_term.Add("supply_time", time);
                    }
                    if (!weight_remain.IsEmpty()) {
                        dic_term.Add("weight_remain", weight_remain.ToInt());
                    }
                }
                if (dic_term.Count > 0) {
                    db.ResetParam().AddParam("@termid", terminal.id).AddParam("@pro", dic_term.JSON()).Execute("update  terminal set properties=@pro  where id=@termid limit 1");
                }
                //ds = db.ResetParam().ExecuteItems("select no,'' pid,'' pno,'' pname,'' weight,'' alarm,'' alarm_time,properties from terminal where type=51 and state=1 order  by no");
            });
            //board_list_rds(1);
            return this;
        }

        /// <summary>
        /// 换菜
        /// </summary>
        /// <param name="prod"></param>
        /// <returns></returns>
        public Gage Prod_Set(string prod) {
            if (!prod.IsJSON()) return Error(3, 0, "prod not json");
            DBItems ds = new DBItems();
            DB.Using(db => {
                string term_pro = db.ResetParam().AddParam("@termid", terminal.id).ExecuteString("select properties from terminal where id=@termid limit 1");
                Dictionary<string, object> dic_term = new Dictionary<string, object> { };
                string time = "2000-01-01 00:00:00";
                if (!term_pro.IsEmpty()) {
                    dic_term = term_pro.ToJDict();
                    if (dic_term.ContainsKey("prod")) {
                        dic_term["prod"] = prod.ToJDict();
                    }
                    else {
                        dic_term.Add("prod", prod.ToJDict());
                    }
                    //if (dic_term.ContainsKey("supply")) {
                    //    dic_term["supply"] = 0;
                    //}
                    //else {
                    //    dic_term.Add("supply", 0);
                    //}
                    if (dic_term.ContainsKey("supply_time")) {
                        dic_term["supply_time"] = time;
                    }
                    else {
                        dic_term.Add("supply_time", time);
                    }
                }
                else {
                    dic_term.Add("prod", prod.ToJDict());
                }
                db.ResetParam().AddParam("@termid", terminal.id).AddParam("@pro", dic_term.JSON()).Execute("update  terminal set properties=@pro  where id=@termid limit 1");
                ds = db.ResetParam().ExecuteItems("select no,'' pid,'' pno,'' pname,'' weight,'' alarm,'' alarm_time,properties from terminal where type=51 and state=1 order  by no");
            });
            //board_list_rds(1);
            return this;
        }
        /// <summary>
        /// 看板
        /// </summary>
        /// <param name="type">1 余量看板</param>
        /// <param name="date">时间</param>
        /// <returns></returns>
        public Gage Board_List(int type, int shopid) {
            //if (!date.IsDateTime()) return Error(3, 0, "invalid date");
            //DBItems ds = new DBItems();
            //string redis_summary = string.Empty;
            //string redis_weight_list = string.Empty;
            //Redis.Using(rd => {
            //    rd.Use(1);
            //    redis_summary = rd.Get("board:gage:weight_summary");
            //    redis_weight_list = rd.Get("board:gage:weight_list");
            //});
            //if (!redis_summary.IsEmpty() || !redis_weight_list.IsEmpty()) {
            //    param.summary= redis_summary.IsEmpty()? new { }: redis_summary.ToJDict();
            //    param.weight_list = redis_weight_list.IsEmpty()? ds.Extend(): ((DBItems)redis_weight_list.Deserialize()).Extend();
            //    return this;
            //}
            if (board_list_rds(type,shopid).GetError(out err)) return Error();
            return this;
        }
        /// <summary>
        /// 看板redis更新
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private Gage board_list_rds(int type,int shopid, int sort = 1) {
            DBItems ds = new DBItems();
            DBItems ds_tray = new DBItems();
            int tray_qty_sum = 80;
            //int tray_timeinterval = 30;
            int tray_alarm_qty = 10;
            int plate_weight = 0;
            DB.Using(db => {
                db.ResetParam();
                string sql_ds = "select no,name,'' pid,'' pno,'' pname,'' weight,'' alarm,'' alarm_time,online,properties from terminal where type=51 and state=1 ";
                string sql_ds_tray = "select no,name, 80 qty_sum, 80 qty,999 min,0 alarm,online,properties from terminal where type=12 and state=1 ";
                if (shopid >= 100) {
                    sql_ds += " and shopid=@shop ";
                    sql_ds_tray += "and shopid=@shop ";
                    db.AddParam("shop", shopid);
                }
                sql_ds += " order by no";
                sql_ds_tray += " order by no";
                ds = db.ExecuteItems(sql_ds);
                ds_tray = db.ExecuteItems(sql_ds_tray);
                tray_qty_sum = db.GetConfig("gage_tray_capacity", "80").ToInt(80);
                //tray_timeinterval = db.GetConfig("gage_tray_timeinterval","30").ToInt(30);
                tray_alarm_qty = db.GetConfig("gage_alarm_tray", "10").ToInt(10);
                plate_weight = (int)double.Parse(db.GetConfig("plate_weight", "0"));
            });
            int weight_normal = 0;
            int weight_alarm = 0;
            int weight_online = 0;
            int tray_normal = 0;
            int tray_alarm = 0;
            int tray_online = 0;
            Dictionary<string, object> summary = new Dictionary<string, object>() { { "person_flow", 0 },  { "weight_all", 0 }, { "weight_online", 0 }, { "weight_normal", 0 }, { "weight_alarm", 0 }, { "tray_all", 0 }, { "tray_online", 0 }, { "tray_normal", 0 }, { "tray_alarm", 0 } };
            switch (type) {
                case 1:
                    if (ds.Length <= 0 && ds_tray.Length <= 0) {
                        param.summary = summary.JSON().ToJDict();
                        param.weight_list = ds.Extend();
                        param.tray_list = ds_tray.Extend();
                        return this;
                    }
                    #region 智能秤
                    List<string> arr = new List<string>();
                    string[] rec;
                    DateTime alarm_time = new DateTime(2000, 01, 01, 00, 00, 00);
                    ds.val.ToList().ForEach(s => {
                        string pid = string.Empty;
                        string pno = string.Empty;
                        string pname = string.Empty;
                        string weight = string.Empty;
                        string alarm = string.Empty;
                        rec = s.Split(DB.SEPAR);
                        string pro = rec[9];
                        Dictionary<string, object> dic_term = new Dictionary<string, object> { };
                        if (!pro.IsEmpty()) {
                            dic_term = pro.ToJDict();
                            if (dic_term.ContainsKey("supply")) alarm = dic_term["supply"].ToString();
                            if (dic_term.ContainsKey("supply_time")) alarm_time = dic_term["supply_time"].ToDateTime();//.ToString("yyyy-MM-dd HH:mm:ss");
                            if (dic_term.ContainsKey("weight_remain")) weight = dic_term["weight_remain"].ToString();
                            if (plate_weight > 0 && weight.IsInt()) weight = Math.Max(weight.ToInt() - plate_weight, 0).ToString();
                            if (dic_term.ContainsKey("prod")) {
                                if (dic_term["prod"].JSON() != "") {
                                    Dictionary<string, object> prod = dic_term["prod"].JSON().ToJDict();
                                    if (prod.ContainsKey("pid")) pid = prod["pid"].ToString();
                                    if (prod.ContainsKey("pno")) pno = prod["pno"].ToString();
                                    if (prod.ContainsKey("pname")) pname = prod["pname"].ToString();
                                }
                            }
                        }
                        string online = rec[8];
                        //if (pid.IsEmpty() || pid == "0") return;
                        if (online.ToInt() == 1) {
                            weight_online++;
                            if (alarm.ToInt() == 1) weight_alarm++;
                            else weight_normal++;
                        }
                        arr.Add(new string[] { rec[0].ToString(), rec[1].ToString(), pid, pno, pname, weight, alarm, alarm_time.ToFloor(), online }.Join(DB.SEPAR));
                    });
                    arr = arr.OrderByDescending(s => s.Split(DB.SEPAR)[8])//在线的在前
                        .ThenBy(s => (s.Split(DB.SEPAR)[2].IsEmpty() ? 1 : 0)) //有菜品的在前
                        .ThenByDescending(s => s.Split(DB.SEPAR)[6].ToInt()).ThenBy(s => s.Split(DB.SEPAR)[7])//有预警的在前
                        .ThenBy(s => s.Split(DB.SEPAR)[0])//最后按照终端编号
                       // .Select(s => s.Substring(0, s.LastIndexOf((DB.SEPAR))))
                        .ToList();
                    ds.val = arr.ToArray();
                    ds.key = ds.key.Replace("properties", "");
                    #endregion
                    #region 托盘机
                    List<string> arr_tray = new List<string>();
                    string[] rec_tray;
                    ds_tray.val.ToList().ForEach(s => {
                        rec_tray = s.Split(DB.SEPAR);
                        string pro = rec_tray[7];
                        Dictionary<string, object> dic_term = new Dictionary<string, object> { };
                        int tray_qty = -1;
                        int alarm_tray = 0;
                        string tray_min = "999";
                        if (!pro.IsEmpty()) {
                            dic_term = pro.ToJDict();
                            if (dic_term.ContainsKey("trayqty")) tray_qty = dic_term["trayqty"].ToInt();
                            if (dic_term.ContainsKey("traymin")) tray_min = dic_term["traymin"].Tostring();
                            if (tray_qty != -1 && tray_qty < tray_alarm_qty) alarm_tray = 1;

                        }
                        string online = rec_tray[6];
                        if (online.ToInt() == 1) {
                            tray_online++;
                            if (alarm_tray == 1) tray_alarm++;
                            else tray_normal++;
                        }
                        double t_m_d = 999;
                        double.TryParse(tray_min, out t_m_d);
                        string t_m_str = (tray_min.IsEmpty() ? "999" : Math.Round(t_m_d,0).ToString());
                        arr_tray.Add(new string[] { rec_tray[0].ToString(), rec_tray[1].ToString(),tray_qty_sum.ToString(), (tray_qty < 0 ? 0 : tray_qty).ToString(), t_m_str, alarm_tray.ToString(), online }.Join(DB.SEPAR));
                    });
                    ds_tray.val = arr_tray.ToArray();
                    ds_tray.key = ds_tray.key.Replace("properties", "");
                    #endregion
                    summary["weight_all"] = ds.Length;
                    summary["weight_online"] = weight_online;
                    summary["weight_normal"] = weight_normal;
                    summary["weight_alarm"] = weight_alarm;
                    param.weight_list = ds.Extend();
                    summary["tray_all"] = ds_tray.Length;
                    summary["tray_online"] = tray_online;
                    summary["tray_normal"] = tray_normal;
                    summary["tray_alarm"] = tray_alarm;
                    param.tray_list = ds_tray.Extend();
                    param.summary = summary.JSON().ToJDict();
                    //Redis.Using(rd => {
                    //    rd.Use(1);
                    //    rd.Set("board:gage:weight_summary", summary.JSON(),TimeSpan.FromMinutes(5));
                    //    rd.Set("board:gage:weight_list", ds.Serialize(), TimeSpan.FromMinutes(5));
                    //});
                    break;
                default:
                    return Error(3, "invalid type");
            }
            return this;
        }
        public Gage weight_update(string weight_remain) {
            DateTime now = DateTime.Now;
            DB.Using(db => {
                string term_pro = db.ResetParam().AddParam("@termid", terminal.id).ExecuteString("select properties from terminal where id=@termid limit 1");
                Dictionary<string, object> dic_term = new Dictionary<string, object> { };
                if (!term_pro.IsEmpty()) {
                    dic_term = term_pro.ToJDict();
                    if (dic_term.ContainsKey("weight_remain")) {
                        dic_term["weight_remain"] = weight_remain.ToInt();
                    }
                    else {
                        dic_term.Add("weight_remain", weight_remain.ToInt());
                    }
                }
                else {
                    dic_term.Add("weight_remain", weight_remain.ToInt());
                }
                if (dic_term.Count > 0) {
                    db.ResetParam().AddParam("@termid", terminal.id).AddParam("@pro", dic_term.JSON()).Execute("update  terminal set properties=@pro  where id=@termid limit 1");
                }
            });
            return this;
        }
        public Gage Term_Prodget(string pids) {
            using (DB db = new DB(snapshot: true)) {
                param.stallid = "";
                param.mode = 0;
                List<string> pidlist = new List<string> { };
                if (pids.IsEmpty()) {
                    int pid = 0;
                    string term_pro = db.ResetParam().AddParam("@termid", terminal.id).ExecuteString("select properties from terminal where id=@termid limit 1");
                    if (term_pro.IsEmpty()) return Error(4, 2, "term_pro empty");
                    var dic_term = term_pro.ToJDict();
                    if (dic_term.ContainsKey("gage_mode")) {
                        param.mode = (dic_term["gage_mode"] + "");
                    }
                    if (!dic_term.ContainsKey("prod") || dic_term["prod"].JSON().IsEmpty()) return Error(4, 2, "term_pro prod empty");//,"未找到菜品"
                    var term_prod = dic_term["prod"].JSON().ToJDict();
                    if (!term_prod.ContainsKey("pid")) return Error(4, 2, "term_pro prod pid empty"); // , "未找到菜品"
                    pid = int.Parse(term_prod["pid"] + "");
                    if (pid <= 0) return Error(4, 2, "term_pro prod pid 0"); //,"未找到菜品"
                    pidlist.Add(pid.ToString());
                }else {
                    pidlist = pids.Split(",").ToList();
                }
                if (terminal.shop.id < 100) return Error(6, "term error.shop." + terminal.shop.id);

                #region 取排菜策略
                Policy p = new Policy().SetTerminal(terminal);
                p = p.CheckFoodPrice(db, terminal.shop.id, DateTime.Now);
                if (p.GetError(out err)) return Error(4, 2, $"policy not found code.{err.Code},sub_code.{err.SubCode},msg.{err.Message}");//,"找不到排菜策略" 找不到策略 返回菜品不存在
                int policyid = p.id;
                if (policyid <= 0) return Error(4, 2, "policy not found -policy ." + policyid);//, "找不到排菜策略"   找不到策略 返回菜品不存在
                #endregion

                string prod_table = "prod";
                string cate_table = "prod_cate";
                if (db.ExistsTable($"prod{terminal.shop.id}")) prod_table = "prod" + terminal.shop.id;
                if (db.ExistsTable($"prod_cate{terminal.shop.id}")) cate_table = "prod_cate" + terminal.shop.id;
                List<gageprod> prodlist = new List<gageprod> { };
                for (int i = 0; i < pidlist.Count; i++) {
                    gageprod item = new gageprod();
                    int pid = pidlist[i].ToInt();
                    DBItems prod = db.ResetParam().AddParam("id", pid).ExecuteItems($"select p.`id`,p.no,p.`name`,p.`state`,p.`remark`,p.`properties`,p.`price`,p.`unit`,p.cate,ISNULL(c.no,'') cateno,ISNULL(c.name,'') catename from  {prod_table} p left join {cate_table} c on p.cate=c.id where p.`id`=@id and p.`type`=111");
                    if (prod.Length <= 0) return Error(4, 2, "prod not found." + pid);
                    DBItems ds_prod = db.GetTable(prod_table, new { id = pid }, limit: 1);
                    if (ds_prod.Length > 0) {
                        Redis.Using(rd => {
                            rd.Use(1).Set(prod_table + ":" + pid, ds_prod.Serialize());
                        });
                    }
                    if (prod[0]["state"].ToInt() != 1) return Error(4, 3, "prod disabled." + pid);



                    #region 每份价格price
                    string sql = @"select  p.type,p.`price`,p.`times`,p.windowid,p.weight,p.qty_book  weight_offset
                                from dish_prices p
                                where p.`type` in(4,5) and p.`state` < 11  and p.policy = @policy and p.id=@pid";
                    DBItems price_db = db.ResetParam().AddParam("@policy", DBType.Int, policyid).AddParam("@pid", DBType.Int, pid).ExecuteItems(sql);
                    if (price_db.Length <= 0) return Error(4, 2, "prod not found -policy." + policyid);//找不到策略 返回菜品不存在
                    bool isexist = false;
                    int part = getPart(db);
                    if (part >= 0) {
                        for (int j = 0; j < price_db.Length; j++) {
                            if (price_db[j]["times"].ToInt() == part) {
                                int type = price_db[j]["type"].ToInt();
                                int weight = price_db[j]["weight"].ToInt();
                                double price = price_db[j]["price"].ToDouble();
                                if (type == 4 && weight == 0) weight = 100;
                                //price = Math.Round(price / weight * 100, 2);//转换成每100g的价格
                                isexist = true;
                                param.price = item.price = price.ToString();
                                param.part =  part;
                                param.stallid = item.stallid = price_db[j]["windowid"];
                                param.type = item.type = type;
                                param.weight = item.weight = weight;
                                param.weight_offset = item.weight_offset = price_db[j]["weight_offset"].ToInt();
                                break;
                            }
                        }
                    }
                    #endregion
                    if (!isexist) return Error(4, 10, $"not in correct part,part.{part}");//, "餐别内未找到有效菜品"   非营业时间段

                    param.id = item.id = prod[0]["id"];
                    param.name = item.name = prod[0]["name"];
                    param.no = item.no = prod[0]["no"];
                    param.cate = item.cate = prod[0]["cate"];
                    param.cateno = item.cateno = prod[0]["cateno"];
                    param.catename = item.catename = prod[0]["catename"];
                    param.remark = item.remark = prod[0]["remark"];
                    param.unit = item.unit = prod[0]["unit"];
                    string properties_str = prod[0]["properties"];
                    var properties = properties_str.ToJDict();
                    #region 营养nutrition
                    dynamic nutrtion_100 = "";
                    double ener_t = 0; double pro_t = 0; double fat_t = 0; double car_t = 0; double na_t = 0;
                    string dbexpand = "[{\"item\":\"能量\",\"value\":0, \"unit\":\"kcal\"},{\"item\":\"蛋白质\",\"value\":0, \"unit\":\"g\"},{\"item\":\"脂肪\",\"value\":0, \"unit\":\"g\"},{\"item\":\"碳水化合物\",\"value\":0, \"unit\":\"g\"},{\"item\":\"钠\",\"value\":0, \"unit\":\"mg\"}]";
                    var obj = dbexpand.ToJDictArray();
                    if (properties != null) {
                        if (properties.ContainsKey("nutrition_100")) {
                            nutrtion_100 = properties["nutrition_100"];
                            ener_t = nutrtion_100.ContainsKey("energy_kcal") ? double.Parse(nutrtion_100["energy_kcal"]) : 0;
                            pro_t = nutrtion_100.ContainsKey("protein") ? double.Parse(nutrtion_100["protein"]) : 0;
                            fat_t = nutrtion_100.ContainsKey("fat") ? double.Parse(nutrtion_100["fat"]) : 0;
                            car_t = nutrtion_100.ContainsKey("cho") ? double.Parse(nutrtion_100["cho"]) : 0;
                            na_t = nutrtion_100.ContainsKey("na") ? double.Parse(nutrtion_100["na"]) : 0;
                        }
                        else if (properties.ContainsKey("nutrition")) {
                            dynamic nutrtion = properties["nutrition"];
                            double weight = properties.ContainsKey("weight") ? double.Parse(properties["weight"].ToString() == "" ? "100" : properties["weight"].ToString()) : 100;
                            double nutrition_rate = 100 / weight;
                            ener_t = double.Parse(nutrtion["energy_kcal"]) * nutrition_rate;
                            pro_t = double.Parse(nutrtion["protein"]) * nutrition_rate;
                            fat_t = (double.Parse(nutrtion["fat"]) * nutrition_rate);
                            car_t = (double.Parse(nutrtion["cho"]) * nutrition_rate);
                            na_t = (double.Parse(nutrtion["na"]) * nutrition_rate);
                        }
                    }
                    obj[0]["value"] = ener_t;
                    obj[1]["value"] = pro_t;
                    obj[2]["value"] = fat_t;
                    obj[3]["value"] = car_t;
                    obj[4]["value"] = na_t;
                    param.nutrition = item.nutrition = obj;
                    #endregion
                    #region 标签tag
                    List<tagmode> tag_list = new List<tagmode> { };
                    if (properties != null) {
                        string tag = "";
                        string[] tag_arr;
                        if (properties.ContainsKey("labs_prod1")) {
                            tag = properties["labs_prod1"].Tostring();
                            tag_arr = tag.Split(',');
                            foreach (var it in tag_arr) {
                                if (!it.IsEmpty()) {
                                    tag_list.Add(new tagmode {
                                        tag = 1,
                                        name = it,
                                        color_back = db.GetConfig("gage_color1_back").IsEmpty() ? tag_color_back[1] : db.GetConfig("gage_color1_back"),
                                        color_text = db.GetConfig("gage_color1_text").IsEmpty() ? tag_color_text[1] : db.GetConfig("gage_color1_text")
                                    });
                                }
                            }
                        }
                        if (properties.ContainsKey("labs_prod2")) {
                            tag = properties["labs_prod2"].Tostring();
                            tag_arr = tag.Split(',');
                            foreach (var it in tag_arr) {
                                if (!it.IsEmpty()) {
                                    tag_list.Add(new tagmode {
                                        tag = 2,
                                        name = it,
                                        color_back = db.GetConfig("gage_color2_back").IsEmpty() ? tag_color_back[2] : db.GetConfig("gage_color2_back"),
                                        color_text = db.GetConfig("gage_color2_text").IsEmpty() ? tag_color_text[2] : db.GetConfig("gage_color2_text")
                                    });
                                }
                            }
                        }
                        if (properties.ContainsKey("labs_prod3")) {
                            tag = properties["labs_prod3"].ToString();
                            tag_arr = tag.Split(',');
                            foreach (var it in tag_arr) {
                                if (!it.IsEmpty()) {
                                    tag_list.Add(new tagmode {
                                        tag = 3,
                                        name = it,
                                        color_back = db.GetConfig("gage_color3_back").IsEmpty() ? tag_color_back[3] : db.GetConfig("gage_color3_back"),
                                        color_text = db.GetConfig("gage_color3_text").IsEmpty() ? tag_color_text[3] : db.GetConfig("gage_color3_text")
                                    });
                                }
                            }
                        }
                    }
                    param.tag = item.tag = tag_list.JSON().ToJDict();
                    #endregion
                    #region 过敏源
                    if (properties != null && properties.ContainsKey("allergy")) {
                        param.allergy = item.allergy = properties["allergy"].ToString();
                    }
                    #endregion
                    prodlist.Add(item);
                }
                param.prodlist = prodlist;
            }
            return this;
        }
        static Dictionary<int, string> tag_color_back = new Dictionary<int, string>() { { 1, "#2800DB63" }, { 2, "#28F2C600" }, { 3, "#28F10027" } };
        static Dictionary<int, string> tag_color_text = new Dictionary<int, string>() { { 1, "#00DB63" }, { 2, "#F2C600" }, { 3, "#F10027" } };
    }
    public class gage_dish {
        public string seq ;
        public int id;
        public string name ;
        public int amt;
        public int weight;
    }
    public class unconscious_dish {
        public string uid = "";
        public int pid;
        public string pno = "";
        public string pname = "";
        public string cate { get; set; }
        public string cateno = "";
        public string catename = "";
        public int stall;
        public int price;
        public int amt;
    }
    public class tagmode{
        public int tag;
        public string name;
        public string color_back = "";
        public string color_text = "";
    }
    public class gageprod {
        public string stallid;
        public int type;
        public string id;
        public string no;
        public string name;
        public string cate;
        public string cateno;
        public string catename;
        public string price;
        public int weight;
        public int weight_offset;
        public string unit;
        public string allergy;
        public string remark;
        public dynamic tag;
        public dynamic nutrition;
    }
}