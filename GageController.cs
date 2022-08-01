using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Sovell.API.Models;

/*
智能计量选餐系统接口
create by rl 2018-07-24
*/

namespace Sovell.API.Controllers {
    public class GageController : BaseController {

        private bool DBError(int code = 0, int subcode = 0, string msg = null) {
            if (code > 0) e.Code = code;
            if (subcode > 0) e.SubCode = subcode;
            if (!msg.IsEmpty()) e.Message = msg;
            return false;
        }
        /// <summary>
        /// 获取菜品列表 仅限终端调用 不允许中心调用
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult prod_list() {
            if (!VerifyData(Request.Form, "")) return Error();
            var mode = new Gage(terminal);
            DBItems list;
            if (mode.prod_list(out list).GetError(out e)) return Error();
            json.prod = list.Extend();
            return Success();
        }
        /// <summary>
        /// 获取菜品信息 仅限终端调用 不允许中心调用
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult prod_get() {
            //VerifySign = false;
            if (!VerifyData(Request.Form, "id")) return Error();
            var mode = new Gage(terminal);
            if (mode.prod_get(args.Get("id").Tostring()).GetError(out e)) return Error();
            json.id = mode.param.id;
            json.name = mode.param.name;
            json.unit = mode.param.unit;
            json.part = 1;
            json.price = 0;
            json.tag = new object[0];
            json.remark = mode.param.remark;
            json.nutrition = new object[0];
            if (mode.param.Exists("part")) {
                json.part = mode.param.part;
            }
            if (mode.param.Exists("price")) {
                json.price = mode.param.price;
            }
            if (mode.param.Exists("tag")) {
                json.tag = mode.param.tag;
            }
            if (mode.param.Exists("nutrition")) {
                json.nutrition = mode.param.nutrition;
            }
            return Success();
        }
        /// <summary>
        /// 根据托盘获取菜品信息-结算台调用
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult dish_get() {
            if (!VerifyData(Request.Form, "uid")) return Error();
            dynamic list = null;
            var mode = new Gage(terminal);
            if (mode.dish_get(args.Get("uid"), out list).GetError(out e)) return Error();
            //json.list = "[{\"id\":1, \"name\":\"菜品1\",\"price\":10,\"seq\":\"1232130\"},{\"id\":2, \"name\":\"菜品2\",\"price\":10,\"seq\":\"123213123120\"}]".JSONDecode<dynamic>();
            json.list = list;
            return Success();
        }
        /// <summary>
        ///结算台结账之后的处理 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult dish_archiv() {
            if (!VerifyData(Request.Form, "uid,seq_list,invoice")) return Error();
            var mode = new Gage(terminal);
            if (mode.dish_archiv(args.Get("uid"), args.Get("seq_list"), args.Get("invoice")).GetError(out e)) return Error();
            return Success();
        }
        
        /// <summary>
        /// 结算异常列表
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult anomalous_list() {
            if (!VerifyData(Request.Form, "pageindex,pagesize")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();

                long sseq = 0;
                long eseq = 0;

                int pageindex = args.Get("pageindex").ToInt(0);
                int pagesize = args.Get("pagesize").ToInt(15);

                var page_first = pageindex * pagesize;
                var page_end = pagesize;

                var shopid = -1;
                var filter = "";
                var tw = "";
                if (args.Exists("shopid")) {
                    shopid = args.Get("shopid").ToInt();

                }
                if (shopid > 0) {
                    tw += " and t.shop =@shopid";
                    db.AddParam("@shopid", shopid);
                }
                if (args.Exists("filter")) {
                    filter = args.Get("filter");
                }
                if (!string.IsNullOrEmpty(filter)) {
                    // filter = filter + "%";
                    tw += " and (t.cardno=@filter or t.profilename=@filter or t.profilemob=@filter)";
                    db.AddParam("@filter", filter);
                }


                if (args.Exists("stime") && !string.IsNullOrEmpty(args.Get("stime"))) {
                    sseq = DateTime.Parse(args.Get("stime")).Ticks;
                    tw += " and t.seq > @sseq ";
                    db.AddParam("@sseq", sseq);
                }

                if (args.Exists("etime") && !string.IsNullOrEmpty(args.Get("etime"))) {
                    eseq = DateTime.Parse(args.Get("etime")).AddDays(1).Ticks;
                    tw += " and t.seq < @eseq ";
                    db.AddParam("@eseq", eseq);
                }

                var sql = " select t.seq,t.shop,t.term,t.card_level,t.uid,t.qty,t.profilemob,t.create_date,t.amt,t.last_update,t.properties,t.cardno from dish_trade_pending t "
                    + " where t.type=6 and t.state=10 " + tw + " order by t.seq desc limit " + page_first + "," + page_end + "; ";

                db.SetSQL(sql);
                //MvcApplication.Log(sql);
                var list = db.ExecuteItems().Extend();
                json.list = list;

                string sum_sql = " select count(0) as total from dish_trade_pending t  where t.type=6 and t.state=10 " + tw;
                db.SetSQL(sum_sql);

                var sum_list = db.ExecuteItems();
                json.total = Convert.ToInt32(sum_list[0]["total"].ToString());
            }
            return Success();
        }
        /// <summary>
        /// 异常结算查询
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult anomalous_sell_list() {
            if(!VerifyData(Request.Form, "pageindex,pagesize")) return Error();
            using(DB db = new DB()) {
                db.ResetParam();

                long sseq = 0;
                long eseq = 0;

                int pageindex = args.Get("pageindex").ToInt(0);
                int pagesize = args.Get("pagesize").ToInt(15);

                var page_first = pageindex * pagesize;
                var page_end = pagesize;

                var shopid = -1;
                var type = -1;
                var filter = "";
                var tw = "";
                if(args.Exists("shopid")) {
                    shopid = args.Get("shopid").ToInt();

                }
                if(shopid > 0) {
                    tw += " and t.shop =@shopid";
                    db.AddParam("@shopid", shopid);
                }
                if(args.Exists("filter")) {
                    filter = args.Get("filter");
                }
                if(!string.IsNullOrEmpty(filter)) {
                    // filter = filter + "%";
                    tw += " and (t.cardno=@filter or t.profilename=@filter or t.profilemob=@filter)";
                    db.AddParam("@filter", filter);
                }


                if(args.Exists("stime") && !string.IsNullOrEmpty(args.Get("stime"))) {
                    sseq = DateTime.Parse(args.Get("stime")).Ticks;
                    tw += " and t.seq > @sseq ";
                    db.AddParam("@sseq", sseq);
                }

                if(args.Exists("etime") && !string.IsNullOrEmpty(args.Get("etime"))) {
                    eseq = DateTime.Parse(args.Get("etime")).Ticks;
                    tw += " and t.seq < @eseq ";
                    db.AddParam("@eseq", eseq);
                }

                if(args.Exists("type")) {
                    type = args.Get("type").ToInt();

                }
                if(type > 0) {
                    tw += " and t.type =@type";
                    db.AddParam("@type", type);
                }

                var sql = " select t.seq,t.shop,t.term,t.card_level,t.uid,t.qty,t.profilemob,t.create_date,t.amt,t.last_update,t.properties,t.cardno from dish_trade_pending t "
                    + " where  t.state=10 " + tw + " order by t.seq desc limit " + page_first + "," + page_end + "; ";

                db.SetSQL(sql);

                var list = db.ExecuteItems().Extend();
                json.list = list;

                string sum_sql = " select count(0) as total from dish_trade_pending t  where t.state=10 " + tw;
                db.SetSQL(sum_sql);

                var sum_list = db.ExecuteItems();
                json.total = Convert.ToInt32(sum_list[0]["total"].ToString());
            }
            return Success();
        }

        /// <summary>
        /// 异常明细
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult anomalous_detail() {
            if (!VerifyData(Request.Form, "seq")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();
                var seq = args.Get("seq").ToLong();

                db.AddParam("@seq", seq);
                if (!db.ExecuteExists("select 1 from dish_trade_pending_details where  seq = @seq limit 1")) return Error(4);

                var sql = " select t.seq,t.shop,t.name,t.price,t.amt,t.weight,t.pno,t.pid from dish_trade_pending_details t where t.seq =@seq  order by pid;";

                db.SetSQL(sql);
                var list = db.ExecuteItems().Extend();
                json.list = list;
            }
            return Success();
        }

        /// <summary>
        /// 异常免单
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult anomalous_free() {
            if (!VerifyData(Request.Form, "seq")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();
                var seq = args.Get("seq").ToLong();
                db.AddParam("@seq", seq);
                if (!db.ExecuteExists("select 1 from dish_trade_pending where  seq = @seq limit 1")) return Error(4);

                var sql = "update dish_trade_pending set state=2 where seq =@seq ";

                var r = db.Execute(sql);
                if (r != 1) {
                    return Error(2, "update fail");
                }

                db.Commit();
            }
            return Success();
        }

        /// <summary>
        /// 再结算 修改单据state使其自动加入结算队列中
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult anomalous_sell() {
            if (!VerifyData(Request.Form, "seq")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();
                var seq = args.Get("seq").ToLong();
                db.AddParam("@seq", seq);
                if (!db.ExecuteExists("select 1 from dish_trade_pending where  seq = @seq limit 1")) return Error(4);
                var sql = "update dish_trade_pending set state=0 where seq =@seq ";
                var r = db.Execute(sql);
                if (r != 1) {
                    return Error(2, "update fail");
                }

                db.Commit();
            }
            return Success();
        }

        /// <summary>
        /// 微信消息记录
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult trade_wechat_mes() {
            if (!VerifyData(Request.Form, "seq,templateid,encode")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();
                var seq = args.Get("seq").ToLong();

                db.AddParam("@seq", seq);
                if (!db.ExecuteExists("select 1 from dish_trade_pending where  seq = @seq limit 1")) return Error(4);

                var sql = "select seq,create_date,cardid,cardno,profileid profile,shop,term,amt from dish_trade_pending where seq =@seq  limit 1 ";
                db.SetSQL(sql);
                var item = db.ExecuteItems();
                var content = "";

                var templateid = args.Get("templateid");
                if (args.Exists("content")) {
                    content = args.Get("content");
                }
                var encode = args.Get("encode").ToInt();
                var cid = item[0]["cardid"];
                var cno = item[0]["cardno"];
                var profile = item[0]["profile"].ToInt();
                var shop = item[0]["shop"].ToInt();
                var term = item[0]["term"].ToInt();
                var amt = item[0]["amt"].ToDouble();
                var create_date = item[0]["create_date"].ToDateTime();

                var messdate = create_date.ToString("yyyyMMdd").ToInt();

                db.AddParam("@templateid", templateid);
                db.AddParam("@content", content);
                db.AddParam("@encode", encode);
                db.AddParam("@cid", cid);
                db.AddParam("@cno", cno);
                db.AddParam("@profile", profile);
                db.AddParam("@shop", shop);
                db.AddParam("@term", term);
                db.AddParam("@messdate", messdate);
                db.AddParam("@trade_date", create_date);
                db.AddParam("@amt", DBType.Double, amt);
                var insertsql = "insert into wechat_unissued_message(seq,type,date,profile,cid,cno,messdate,encode,templateid,content,shop,term,amt,trade_date)"
                    + " values(@seq,1,now(),@profile,@cid,@cno,@messdate,@encode,@templateid,@content,@shop,@term,@amt,@trade_date) ";
                var r = db.Execute(insertsql);
                if (r != 1) {
                    return Error(2, "insert fail");
                }
                db.Commit();
            }
            return Success();
        }

        /// <summary>
        /// 申诉记录查询
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult appeal_list() {
            if (!VerifyData(Request.Form, "pageindex,pagesize")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();

                long sseq = 0;
                long eseq = 0;

                int pageindex = args.Get("pageindex").ToInt(0);
                int pagesize = args.Get("pagesize").ToInt(15);

                var page_first = pageindex * pagesize;
                var page_end = pagesize;

                var shopid = -1;
                var filter = "";
                var tw = "";
                if (args.Exists("shopid")) {
                    shopid = args.Get("shopid").ToInt();

                }
                if (shopid > 0) {
                    tw += " and t.shop =@shopid";
                    db.AddParam("@shopid", shopid);
                }
                if (args.Exists("filter")) {
                    filter = args.Get("filter");
                }
                if (!string.IsNullOrEmpty(filter)) {
                    // filter = filter + "%";
                    tw += " and (t.cardno=@filter or t.profilename=@filter or t.profilemob=@filter)";
                    db.AddParam("@filter", filter);
                }


                if (args.Exists("stime") && !string.IsNullOrEmpty(args.Get("stime"))) {
                    sseq = DateTime.Parse(args.Get("stime")).Ticks;
                    tw += " and t.seq >= @sseq ";
                    db.AddParam("@sseq", sseq);
                }

                if (args.Exists("etime") && !string.IsNullOrEmpty(args.Get("etime"))) {
                    eseq = DateTime.Parse(args.Get("etime")).AddDays(1).Ticks;
                    tw += " and t.seq < @eseq ";
                    db.AddParam("@eseq", eseq);
                }

                var sql = " select t.seq,t.shop,t.term,t.profilename,t.profilemob,t.create_date,t.last_update,t.cardno from dish_trade_update_log t "
                    + " where 1=1 " + tw + " order by t.seq desc limit " + page_first + "," + page_end + "; ";

                db.SetSQL(sql);
                //MvcApplication.Log(sql);
                var list = db.ExecuteItems().Extend();
                json.list = list;

                string sum_sql = " select count(0) as total from dish_trade_update_log t  where 1=1 " + tw;
                db.SetSQL(sum_sql);

                var sum_list = db.ExecuteItems();
                json.total = Convert.ToInt32(sum_list[0]["total"].ToString());
            }
            return Success();
        }

        /// <summary>
        /// 申诉记录明细
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult appeal_detail() {
            if (!VerifyData(Request.Form, "seq")) return Error();
            using (DB db = new DB()) {
                db.ResetParam();
                var seq = args.Get("seq").ToLong();

                db.AddParam("@seq", seq);
                if (!db.ExecuteExists("select 1 from dish_trade_details_update_log where  seq = @seq limit 1")) return Error(4);

                var sql = " select t1.state,pid,pname,amt,weight,'/' appeal_amt  from "
                    + " (select state,pid,pname,weight,amt from dish_trade_details_update_log  where state =1 and seq=@seq)t1 "
                    + " union all "
                    + " select t2.state,t2.pid,t2.pname,t12.amt,t2.weight,t2.amt appeal_amt from "
                    + " (select state, pid, pname, sum(weight) weight, sum(amt) amt from dish_trade_details_update_log  where state = 12 and seq=@seq group by state, pid, pname  ) t12"
                    + " left join "
                    + " (select state, pid, pname, weight, amt from dish_trade_details_update_log  where state = 2 and seq=@seq )t2"
                    + " on t12.pid=t2.pid ";

                db.SetSQL(sql);
                var list = db.ExecuteItems().Extend();
                json.list = list;
            }
            return Success();
        }

        /// <summary>
        /// 档口列表获取
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public ContentResult stall_list() {
            if (!VerifyData(Request.Form, "")) return Error();
            var mode = new Gage(terminal);
            DBItems list;
            if (mode.stall_list(out list).GetError(out e)) return Error();
            json.stall = list.Extend();
            return Success();
        }
        /// <summary>
        /// 补菜/取消补菜
        /// by rl 2019-07-12
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Prod_Supply() {
            if (!VerifyData(Request.Form, "")) return Error();
            string state = args.Get("state");
            if (!state.IsEmpty() && !state.ToInt().Between(0, 1)) return Error(3, "invalid state");
            var g = new Gage(terminal);
            if (g.Prod_Supply(state, args.Get("weight_remain")).GetError(out e)) return Error();
            return Success();
        }
        /// <summary>
        /// 换菜
        /// by rl 2019-07-15
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Prod_Set() {
            if (!VerifyData(Request.Form, "prod")) return Error();
            string prod = args.Get("prod");
            if (!prod.IsJSON()) return Error(3, "invalid prod no json");
            var g = new Gage(terminal);
            if (g.Prod_Set(prod).GetError(out e)) return Error();
            return Success();
        }
        /// <summary>
        /// 看板
        /// </summary>
        /// <param name="args">t=1 余量看板</param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Board_List() {
            if (!VerifyData(Request.Form, new Dictionary<int, string>() {
                {1, "" },
            }, 1)) return Error();
            int t = args.Get("t").ToInt(1);
            //string date = args.Get("date");
            int shopid = args.Get("shopid").ToInt();
           // if (!date.IsDateTime()) return Error(3, "invalid date");
            var g = new Gage(terminal);
            if (g.Board_List(t, shopid).GetError(out e)) return Error();
            if (g.param.Exists("summary")) {
                json.summary = g.param.summary;
            }
            if (g.param.Exists("weight_list")) {
                json.weight_list = g.param.weight_list;
            }
            if (g.param.Exists("tray_list")) {
                json.tray_list = g.param.tray_list;
            }
            return Success();
        }

        /// <summary>
        /// 获取终端菜品信息 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult term_prodget() {
            if (!VerifyData(Request.Form, "")) return Error();
            var mode = new Gage(terminal);
            if (mode.Term_Prodget(args.Get("pids")).GetError(out e)) return Error();
            json.stallid = mode.param.stallid;
            json.mode = mode.param.mode;
            string pid = mode.param.id + "";
            json.id = pid;
            json.name = mode.param.name;
            json.price = 0;
            json.part = 1;
            json.unit = mode.param.unit;
            json.tag = new object[0];
            json.allergy = "";
            if (mode.param.Exists("allergy")) {
                json.allergy = mode.param.allergy;
            }
            json.remark = "";
            json.nutrition = new object[0];
            if (mode.param.Exists("part")) {
                json.part = mode.param.part;
            }
            if (mode.param.Exists("prodlist")) {
                json.prodlist = mode.param.prodlist;
            }
            if (mode.param.Exists("price")) {
                json.price = mode.param.price;
            }
            int ptype = mode.param.type;
            if (mode.param.Exists("type")) {
                json.type = ptype;
            }
            if (mode.param.Exists("weight")) {
                int weight = mode.param.weight;
                json.weight = weight;
                Redis.Using(rd => {
                    rd.Use(1);
                    rd.Set($"gage:term_prod_type:{terminal.id}:{pid}", ptype.ToString());
                    rd.Set($"gage:term_prod_weight_each:{terminal.id}:{pid}", weight.ToString());
                });
            }
            if (mode.param.Exists("weight_offset")) {
                json.weight_offset = mode.param.weight_offset;
            }
            if (mode.param.Exists("remark")) {
                json.remark = mode.param.remark;
            }
            if (mode.param.Exists("tag")) {
                json.tag = mode.param.tag;
            }
            if (mode.param.Exists("nutrition")) {
                json.nutrition = mode.param.nutrition;
            }
            if (mode.param.Exists("no")) {
                json.no = mode.param.no;
            }
            if (mode.param.Exists("cate")) {
                json.cate = mode.param.cate;
            }
            if (mode.param.Exists("cateno")) {
                json.cateno = mode.param.cateno;
            }
            if (mode.param.Exists("catename")) {
                json.catename = mode.param.catename;
            }
            return Success();
        }

        /// <summary>
        /// 剩余重量更新
        /// by rl 2021-09-23
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult weight_update() {
            if (!VerifyData(Request.Form, "weight_remain")) return Error();
            var g = new Gage(terminal);
            if (g.weight_update(args.Get("weight_remain")).GetError(out e)) return Error();
            return Success();
        }

        #region 智能秤页面所有接口
        /// <summary>
        ///  智能秤菜品设置数据获取。
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult prod_SetUp_IntelligentScale() {
            if (!VerifyData(Request.Form)) return Error();
            // 页码
            var pageIndex = args.Get("pageIndex").ToInt();
            // 页面大小
            var pageSize = args.Get("pageSize").ToInt();
            var no = args.Get("no");
            var page_First = pageIndex * pageSize;
            var page_End = pageSize;
            // 终端(暂不启用写死成51)
            var term = args.Get("term").ToInt();
            term = 51;
            string sqlStr = string.Empty;
            string sqlStr_Count = string.Empty;
            if (string.IsNullOrEmpty(no)) {
                // 数据查询
                sqlStr = @"select  * from terminal where type = 51 limit " + page_First + "," + page_End + "";
                // 总数查询
                sqlStr_Count = @" select count(*) count from terminal where type = 51";
            }
            else {
                // 数据查询
                sqlStr = @"select  * from terminal where type = 51 and no=@no limit " + page_First + "," + page_End + "";
                // 总数查询
                sqlStr_Count = @" select count(*) count from terminal where type = 51 and no=@no ";
            }
            using (var db = new DB(snapshot: true)) {
                db.AddParam("no", no);
                var data = db.SetSQL(sqlStr).GetData().Extend();
                var data_Count = db.SetSQL(sqlStr_Count).GetData().Extend();
                json.list = data;
                json.total = data_Count[0]["count"];
                return Success();
            }
        }
        /// <summary>
        ///  通过编号修改或者保存 模式的值
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult setting_Mode() {
            if (!VerifyData(Request.Form, "id:int,mode:int")) {
                return Error();
            }
            // 设备编号
            var id = args.Get("id").ToInt();
            // 所属模式
            var mode = args.Get("mode").ToInt();
            using (var db = new DB()) {
                //     var _UserId = db.ExecuteString("select count(1) from my_aspnet_users where LOWER(`Name`)=@UserName ").ToInt();
                // 先从数据库取出 此设备编号保存的信息
                string properties = db.ResetParam().AddParam("@id", id).ExecuteString("select properties from terminal where id=@id");
                Dictionary<string, object> gage_mode = new Dictionary<string, object> { };
                // string properties = string.Empty;
                if (!properties.IsEmpty()) {
                    // 转换
                    gage_mode = properties.ToJDict();
                    // 判断键值是否存在
                    if (gage_mode.ContainsKey("gage_mode")) {
                        // 存在直接赋值 
                        gage_mode["gage_mode"] = mode;
                    }
                    else {
                        // 不存在直接添加
                        gage_mode.Add("gage_mode", mode);
                    }
                }
                else {
                    // 如果属性为空，则直接添加
                    gage_mode.Add("gage_mode", mode);
                }
                var isSuccess = db.ResetParam().AddParam("@id", id).AddParam("@pro", gage_mode.JSON()).Execute("update  terminal set properties=@pro  where id=@id");
                // 提交事务
                db.Commit();
                json.list = isSuccess;
                return Success();

            }
        }
        public ContentResult save_ProdInfoByTerm() {
            if (!VerifyData(Request.Form, "pId:string,pNo:string,pName:string,id:int")) {
                return Error();
            }
            // 菜品ID
            var pId = args.Get("pId");
            // 菜品编号
            var pNo = args.Get("pNo");
            // 菜品名称
            var pName = args.Get("pName");
            // 设备编号
            var id = args.Get("id").ToInt();
            var isSuccess = 0;
            using (var db = new DB()) {
                //     var _UserId = db.ExecuteString("select count(1) from my_aspnet_users where LOWER(`Name`)=@UserName ").ToInt();
                // 先从数据库取出 此设备编号保存的信息
                string properties = db.ResetParam().AddParam("@id", id).ExecuteString("select properties from terminal where id=@id");
                Dictionary<string, object> gage_mode = new Dictionary<string, object> { };
                // string properties = string.Empty;
                Dictionary<string, object> prod = new Dictionary<string, object>();
                prod.Add("pid", pId);
                prod.Add("pno", pNo);
                prod.Add("pname", pName);
                gage_mode = properties.ToJDict();
                if (!properties.IsEmpty()) {
                    #region 判断菜品信息是否存在。
                    // 判断属性中是否有菜品信息的Json字符串（如果有 则修改）
                    if (gage_mode.ContainsKey("prod")) {
                        gage_mode["prod"] = prod;
                    }
                    else {
                        // 如果没有直接添加
                        gage_mode.Add("prod", prod);
                    }
                    #endregion
                }
                else {
                    gage_mode.Add("prod", prod);
                }
                isSuccess = db.ResetParam().AddParam("@id", id).AddParam("@pro", gage_mode.JSON()).Execute("update  terminal set properties=@pro  where id=@id");
                // 提交事务
                db.Commit();

            }
            json.isSuccess = isSuccess;
            return Success();
        }
        /// <summary>
        /// 订单记录查询
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public ContentResult Order_list() {
            if (!VerifyData(Request.Form, new Dictionary<int, string>() {
                {1, "uid"},//原申诉机使用-返回值为array(不合理 待废弃)
                {2, "uid"},//K1自助结算台使用
            }, 1)) return Error(3);
            int t = args.Get("t").ToInt();
            switch (t) {
                case 1:
                case 2:
                    bool ispmcard = false;
                    string uid = args.Get("uid");
                    DBItems ds = new DBItems();
                    DBItems ds_detail = new DBItems();
                    string sql = "select seq,uid,cardid,cardno,profileid,profilename from dish_trade_pending where state=0 and type=6 and  uid=@uid limit 1";
                    DB.Using(db => {
                        Redis.Using(rd => {
                            rd.Use(1);
                            ispmcard = getpmcard(db, rd, uid);
                        });
                        if (ispmcard) return DBError(19, 0, "pmcard not allowed");
                        ds = db.ResetParam().AddParam("@uid", uid).AddParam("@shop",terminal.shop.id).ExecuteItems(sql);
                       
                        if (ds.Length > 0) {
                            ds_detail = db.ResetParam().AddParam("seq", ds[0]["seq"].ToLong()).ExecuteItems("select pid,pno,name,price,pricer,amt,weight,remark,cate,cateno,catename from dish_trade_pending_details where seq=@seq");
                        }
                        return true;
                    });
                    if (e.Code > 1) return Error(e.Code, e.SubCode, e.Message);
                    if (ds.Length <= 0 || ds_detail.Length <= 0) {
                        if (t == 1) {
                            json.order = ds_null.Extend();
                        }
                        else if (t == 2) {
                            json.order = new { };
                        }
                        return Success();
                    }
                    List<proddetail> prod = new List<proddetail> { };
                    for (int i = 0; i < ds_detail.Length; i++) {
                        int pid = ds_detail[i]["pid"].ToInt();
                        string pno = ds_detail[i]["pno"];
                        string pname = ds_detail[i]["name"];
                        ulong price = ds_detail[i]["price"].ToMoney();
                        ulong amt = ds_detail[i]["amt"].ToMoney();
                        int weight = ds_detail[i]["weight"].ToInt();
                        string remark = ds_detail[i]["remark"];
                        int ptype = ds_detail[i]["pricer"].ToInt();
                        ptype = Math.Max(ptype, 4);
                        int weight_each = 0;
                        if (ptype == 4) weight_each = 100;
                        if (!remark.IsEmpty()) {
                            var prope = remark.ToJDict();
                            if (prope.ContainsKey("prod_weight_each")) {
                                weight_each = (int)prope["prod_weight_each"];
                            }
                        }
                        int cateid = ds_detail[i]["cate"].ToInt();
                        string cateno = ds_detail[i]["cateno"];
                        string catename = ds_detail[i]["catename"];
                        if (prod.Count(s => s.pid == pid && s.type == ptype) > 0) {
                            prod.Single(s => s.pid == pid && s.type == ptype).amt += amt;
                            prod.Single(s => s.pid == pid && s.type == ptype).weight += weight;
                        }
                        else {
                            prod.Add(new proddetail() { pid = pid, pno = pno, pname = pname, price = price, amt = amt, weight = weight, type = ptype, weight_each = weight_each, cateid = cateid, cateno = cateno, catename = catename });
                        }

                    }
                    if (t == 1) {
                        json.order = new List<dynamic> { new { seq = ds[0]["seq"].ToLong(), uid = ds[0]["uid"], cardid = ds[0]["cardid"], cardno = ds[0]["cardno"],
                        profileid = ds[0]["profileid"].ToInt(), profilename = ds[0]["profilename"],
                        proddetail = prod.JSON().JSONDecode<List<proddetail>>() } };
                    }
                    else if (t == 2) {
                        json.order = new {
                            seq = ds[0]["seq"].ToLong(),
                            uid = ds[0]["uid"],
                            cardid = ds[0]["cardid"],
                            cardno = ds[0]["cardno"],
                            profileid = ds[0]["profileid"].ToInt(),
                            profilename = ds[0]["profilename"],
                            proddetail = prod.JSON().JSONDecode<List<proddetail>>()
                        };
                    }
                    break;
                default:
                    return Error(3, 0, "invalid type");
            }
            if (e.Code > 1) return Error();
            return Success();
        }
        public bool getpmcard(DB db, Redis rd, string uid) {
            bool rds_pmcard = false;
            bool ispmcard = false;
            string val_pmc = rd.Get("config:tray_pmcard");
            if (!val_pmc.IsEmpty()) {
                rds_pmcard = true;
                if (val_pmc.Split(",").ToList().Count(s => s == uid) > 0) {
                    ispmcard = true;
                }
            }
            if (ispmcard) return true;
            if (rds_pmcard) return ispmcard;
            string pmcard = db.GetConfig("tray_pmcard");
            if (!pmcard.IsEmpty()) {
                if (pmcard.Split(",").ToList().Count(s => s == uid) > 0) {
                    ispmcard = true;
                }
            }
            return ispmcard;
        }
        [HttpPost]
        public ContentResult Order_Update() {
            if (!VerifyData(Request.Form, "seq,prod")) return Error(3);
            long seq = args.Get("seq").ToLong();
            string prod = args.Get("prod");
            DB.Using(db => {
                DBItems ds = db.ResetParam().AddParam("seq", seq).AddParam("@shop", terminal.shop.id).ExecuteItems("select seq,qty,amt,state,remark,uid,shop,last_update from dish_trade_pending where seq=@seq and type=6 and shop=@shop limit 1");
                if (ds.Length <= 0) return DBError(4, 0, "not found");
                int state = ds[0]["state"].ToInt();
                int qty_pre = ds[0]["qty"].ToInt();
                int shopid = ds[0]["shop"].ToInt();
                string prod_table = "prod";
                if (db.ExistsTable(prod_table + shopid)) prod_table = prod_table + shopid;
                ulong amt_pre = ds[0]["amt"].ToMoney();
                string uid = ds[0]["uid"];
                DateTime last_update = ds[0]["last_update"].ToDateTime();
                if (state == 1) return DBError(4, 1, "order payed");
                if (state != 0) return DBError(4, state, "order state error");
                var p_arr = prod.Split(DB.NEWLINE);
                for (int i = 0; i < p_arr.Length; i++) {
                    var pp = p_arr[i].Split(",");
                    if (pp.Length < 3) return DBError(3, 1, "invalid prods (pid,ptype,weight,amt)│");
                    int pid = pp[0].ToInt();
                    int ptype = pp[1].ToInt();
                    int weight = pp[2].ToInt();
                    int amt = pp[3].ToInt();
                    db.ResetParam().AddParam("seq", seq).AddParam("pid", pid).AddParam("ptype", ptype);//.AddParam("weight", weight).AddParam("amt", amt);
                    DBItems ds_d = db.ExecuteItems("select * from dish_trade_pending_details where seq=@seq and pid=@pid and pricer=@ptype");
                    if (ds_d.Length <= 0) return DBError(3, 1, $"invalid prod.{pid}");
                    var keys = ds_d.key.Replace(DB.SEPAR.ToString(), ",").Replace("autoid,", "").Replace(",autoid", "");
                    keys = "INSERT INTO dish_trade_pending_details(" + keys + ") SELECT " + keys.Replace("weight,", (weight).ToString() + ", ").Replace("amt,", (amt * 0.01).ToString() + ", ").Replace("state,", "2, ") + " FROM dish_trade_pending_details WHERE seq=@seq and pid=@pid and pricer=@ptype limit 1;SELECT @@Identity";
                    long autoid = db.ExecuteString(keys).ToLong();
                   // db.AddParam("autoid", autoid).Execute("delete from dish_trade_pending_details where seq=@seq and pid=@pid and pricer=@ptype and autoid!=@autoid");
                   db.AddParam("autoid", autoid).AddParam("alllen", ds_d.Length).Execute("update dish_trade_pending_details set state=12 where seq=@seq and pid=@pid and pricer=@ptype and autoid!=@autoid limit @alllen");
                }
                int qty_all = 0;
                ulong amt_all = 0;
                List<string> pname_list = new List<string> { };
                Dictionary<int, int> prod_weight = new Dictionary<int, int> { };
                DBItems ds_detail = db.ResetParam().AddParam("seq", seq).ExecuteItems("select pid,name, amt,weight from dish_trade_pending_details where  seq=@seq and state<10");
                for(int i = 0; i < ds_detail.Length; i++) {
                    string pname = ds_detail[i]["name"];
                    int pid = ds_detail[i]["pid"].ToInt();
                    int pweight = ds_detail[i]["weight"].ToInt();
                    qty_all++;
                    amt_all += ds_detail[i]["amt"].ToMoney();
                    if (!pname_list.Contains(pname)) pname_list.Add(pname);
                    if (prod_weight.ContainsKey(pid)) {
                        prod_weight[pid] += pweight;
                    }else {
                        prod_weight[pid] = pweight;
                    }
                }
                string remark_prod = pname_list.Take(3).ToArray().Join(",");
                string remark = new Dictionary<string, string> { { "prods", remark_prod } }.JSON();
                db.ResetParam().AddParam("seq", seq).AddParam("qty", qty_all).AddParam("amt", DBType.Money, amt_all * 0.01).AddParam("remark", remark).Update("dish_trade_pending", "seq");       
                #region 订单处理(申诉)日志记录
                if (db.ExistsTable("dish_trade_update_log") && db.ExistsTable("dish_trade_details_update_log")) {
                    var m = new Major(db, terminal);
                    long seq_log = m.Seq;
                    if (m.Used) seq_log = m.GetNext();
                    DateTime now = DateTime.Now;
                    db.ResetParam().AddParam("seq",seq_log).AddParam("type", 1).AddParam("state", 1).AddParam("qty", qty_all).AddParam("amt", amt_all)
                       .AddParam("qty_pre", qty_pre).AddParam("amt_pre", amt_pre).AddParam("now", now).AddParam("invoice", seq)
                       .Execute(@"insert into dish_trade_update_log(seq,type,state,qty,amt,qty_pre,amt_pre,create_date,last_update,invoice,
                        remark,properties,shop,shop_no,shop_name,term,term_no,term_name,operator,uid,part,profileid,profilename,profilemob,cardid,cardno) 
                        select @seq,@type,@state,@qty,@amt,@qty_pre,@amt_pre,@now,create_date,@invoice,remark,properties,shop,shop_no,shop_name,term,term_no,term_name,operator,uid,part,profileid,profilename,profilemob,cardid,cardno from dish_trade_pending where seq=@invoice limit 1");

                    db.ResetParam().AddParam("seq", seq_log).AddParam("now", now).AddParam("invoice", seq)
                       .Execute(@"insert into dish_trade_details_update_log(seq,type,state,create_date,pid,pno,pname,weight,price,amt,cateid,cateno,catename,remark,properties) 
                        select @seq,1,state,create_date,pid,pno,name,weight,cast(price*100 as SIGNED INTEGER),cast(amt*100 as SIGNED INTEGER),cate,cateno,catename,remark,'' from dish_trade_pending_details where seq=@invoice;");
                }
                #endregion
                db.ResetParam().AddParam("seq", seq).Execute("delete from dish_trade_pending_details where seq=@seq and state>10");
                db.ResetParam().AddParam("seq", seq).Execute("update dish_trade_pending_details set state=1 where seq=@seq and state=2");
                Redis.Using(rd => {
                    rd.Use(1);
                    string key_uid = $"profile_tray_relate:uid:{uid}";
                    string val_uid = rd.Get(key_uid);
                    if (val_uid.IsEmpty()) return;
                    profile_tray_relate ptr = val_uid.ToJObject<profile_tray_relate>();
                    ptr.amt_sum = (int)amt_all;

                    double energy_kcal = 0, protein = 0, fat = 0, cho = 0, na = 0;
                    foreach (int key in prod_weight.Keys) {
                        string prod_i = rd.Get(prod_table + ":" + key);
                        int pweight = prod_weight[key];
                        DBItems product = new DBItems();
                        // TODO 
                        //if (!prod_i.IsEmpty()) {
                        //    product = (DBItems)prod_i.Deserialize();
                        //}else {
                        //    product = db.ExecuteItems($"select properties from {prod_table} where id={key} limit 1");
                        //}
                        string properties_str = product[0]["properties"];
                        if (properties_str.IsEmpty()) continue;
                        var properties = properties_str.ToJDict();
                        if (properties == null) continue;
                        #region 营养nutrition
                        dynamic nutrtion_100 = "";
                        double ener_t = 0; double pro_t = 0; double fat_t = 0; double cho_t = 0; double na_t = 0;
                        if (properties.ContainsKey("nutrition_100")) {
                            nutrtion_100 = properties["nutrition_100"];
                            if (nutrtion_100.ContainsKey("energy_kcal")) double.TryParse(nutrtion_100["energy_kcal"], out ener_t);
                            if (nutrtion_100.ContainsKey("protein")) double.TryParse(nutrtion_100["protein"], out pro_t);
                            if (nutrtion_100.ContainsKey("fat")) double.TryParse(nutrtion_100["fat"], out fat_t);
                            if (nutrtion_100.ContainsKey("cho")) double.TryParse(nutrtion_100["cho"], out cho_t);
                            if (nutrtion_100.ContainsKey("na")) double.TryParse(nutrtion_100["na"], out na_t);
                        }
                        else if (properties.ContainsKey("nutrition")) {
                            dynamic nutrtion = properties["nutrition"];
                            double weight = properties.ContainsKey("weight") ? double.Parse(properties["weight"].ToString() == "" ? "100" : properties["weight"].ToString()) : 100;
                            double nutrition_rate = 100 / weight;
                            double.TryParse(nutrtion["energy_kcal"], out ener_t);
                            ener_t = ener_t * nutrition_rate;
                            double.TryParse(nutrtion["protein"], out pro_t);
                            pro_t = pro_t * nutrition_rate;
                            double.TryParse(nutrtion["fat"], out fat_t);
                            fat_t = fat_t * nutrition_rate;
                            double.TryParse(nutrtion["cho"], out cho_t);
                            cho_t = cho_t * nutrition_rate;
                            double.TryParse(nutrtion["na"], out na_t);
                            na_t = na_t * nutrition_rate;
                        }
                        #endregion
                        energy_kcal += ener_t / 100 * pweight; protein += pro_t / 100 * pweight; fat += fat_t / 100 * pweight; cho += cho_t / 100 * pweight; na += na_t / 100 * pweight;
                    }
                    List<profile_tray_relate_expand> list_expand = new List<profile_tray_relate_expand> { };
                    list_expand.Add(new profile_tray_relate_expand() { item = "能量", value = Math.Round(energy_kcal, 2).ToString(), unit = "kcal" });
                    list_expand.Add(new profile_tray_relate_expand() { item = "蛋白质", value = Math.Round(protein, 2).ToString(), unit = "g" });
                    list_expand.Add(new profile_tray_relate_expand() { item = "脂肪", value = Math.Round(fat, 2).ToString(), unit = "g" });
                    list_expand.Add(new profile_tray_relate_expand() { item = "碳水化合物", value = Math.Round(cho, 2).ToString(), unit = "g" });
                    list_expand.Add(new profile_tray_relate_expand() { item = "钠", value = Math.Round(na, 2).ToString(), unit = "mg" });
                    list_expand.Add(new profile_tray_relate_expand() { item = "金额", value = amt_all.ToString(), unit = "分" });
                    ptr.expand = list_expand;
                    int expire = expire = getExpireAutoTime(rd, shopid);//过期时间 默认10分钟;
                    int expired = (int)DateTime.Now.Subtract(last_update).TotalSeconds;
                    rd.Set(key_uid, ptr.JSON(), TimeSpan.FromSeconds(Math.Max(expire - expired, 1)));
                });
                return true;
            });
            if (e.Code > 1) return Error();
            return Success();
        }
        public int getExpireAutoTime(Redis rd, int shop) {
            int expire_def = 10 * 60;//默认10分钟
            int expire = expire_def;
            string val = string.Empty;
            val = rd.Get($"profile_tray_relate:autotime:0");
            if (!val.IsEmpty()) {
                expire = val.ToInt();
                return expire <= 0 ? expire_def : expire;
            }
            val = rd.Get($"profile_tray_relate:autotime:{shop}");
            if (!val.IsEmpty()) {
                expire = val.ToInt();
                return expire <= 0 ? expire_def : expire;
            }
            DBItems ds = ds_null;
            DB.Using(db => {
                ds = db.ExecuteItems("select id,type,name,`trigger`,`action`,`state`,remark,create_date,last_date from policy where type = 42 and state = 1");
            });
            if (ds.Length <= 0) return expire <= 0 ? expire_def : expire;
            string trig = string.Empty;
            string action = string.Empty;
            string shops = string.Empty;
            Dictionary<string, string> jsont = null;
            Dictionary<string, string> jsona = null;
            for (int i = 0; i < ds.Length; i++) {
                trig = ds[i]["trigger"];
                action = ds[i]["action"];
                jsont = (trig.ToJDict()).ToDictionary(d => d.Key, d => d.Value.ToString());
                jsona = (action.ToJDict()).ToDictionary(d => d.Key, d => d.Value.ToString());
                if (!jsont.ContainsKey("11") || jsont["11"].IsEmpty()) continue;
                jsont["11"].Split(',').Where(s => !s.IsEmpty()).ToList().ForEach(s => {
                    if (s.ToInt() == shop) {
                        if (jsona.ContainsKey("11")) {
                            if (jsona["11"].ToInt() == 0) {
                                expire = (int)(DateTime.Now.AddDays(1).ToString("yyyy-MM-dd").ToDateTime().Subtract(DateTime.Now).TotalSeconds);
                            }
                            else if (jsona.ContainsKey("6")) {
                                expire = jsona["6"].ToInt();
                            }
                        }
                        return;
                    }
                });
            }
            int result = expire <= 0 ? expire_def : expire;
            rd.Set($"profile_tray_relate:autotime:{shop}", result.ToString());
            return result;
        }
        public class profile_tray_relate {
            public string uid;
            public int pid;
            public int amt_sum = 0;
            public string date_lastuse;
            public long date_seq;
            public string properties;
            public List<profile_tray_relate_expand> expand {
                get;
                set;
            }
        }
        public class profile_tray_relate_expand {
            public string item;
            public string value;
            public string unit;
        }
        public class proddetail {
            public int pid;
            public string pno;
            public string pname;
            public int type;
            public ulong price;
            public int weight_each;
            public int weight;
            public ulong amt;
            public int cateid;
            public string cateno;
            public string catename;
        }
        DBItems ds_null = new DBItems();
        #endregion
    }
}




