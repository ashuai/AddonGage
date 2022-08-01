DROP PROCEDURE IF EXISTS del_idx;  
create procedure del_idx(IN p_tablename varchar(200), IN p_idxname VARCHAR(200))  
begin  
DECLARE str VARCHAR(250);  
  set @str=concat(' drop index ',p_idxname,' on ',p_tablename);   
  select count(*) into @cnt from information_schema.statistics where TABLE_SCHEMA=DATABASE() and table_name=p_tablename and index_name=p_idxname ;  

  if @cnt >0 then   

    PREPARE stmt FROM @str;  

    EXECUTE stmt ;  

  end if;  
end ;
CREATE TABLE if not exists `gage_trade` ( 
`seq`                         bigint                        NOT NULL, 
`shop`                        int                           NOT NULL DEFAULT 0, 
`term`                        int                           NOT NULL DEFAULT 0, 
`dish_id`                     varchar(20)                   NOT NULL DEFAULT '', 
`cardid`                      varchar(32)                   NOT NULL DEFAULT '', 
`card_level`                  int                           NOT NULL DEFAULT 0, 
`card_group`                  int                           NOT NULL DEFAULT 0, 
`pid`                         varchar(20)                   NOT NULL DEFAULT '', 
`pname`                       varchar(64)                   NOT NULL DEFAULT '', 
`weight`                      int                           NOT NULL DEFAULT 0, 
`part`					      int							NOT NULL DEFAULT 0,
`price`                       int                           NOT NULL DEFAULT 0, 
`amt`                         int                           NULL DEFAULT 0,
`taketime`                    datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP, 
`tick`                        bigint                        NOT NULL DEFAULT 0, 
`create_date`                 datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP,
`cate`                        int                           NULL DEFAULT 0, 
`stall`                       int                           NULL DEFAULT 0,  
CONSTRAINT PK_GAGE_TRADE PRIMARY KEY (`seq`)
);

call del_idx('gage_trade','IX_gage_trade_1');  
ALTER TABLE gage_trade ADD INDEX IX_gage_trade_1 (TICK ASC,DISH_ID ASC);



CREATE TABLE if not exists `gage_trade_archiv` ( 
`seq`                         bigint                        NOT NULL, 
`shop`                        int                           NOT NULL DEFAULT 0, 
`term`                        int                           NOT NULL DEFAULT 0, 
`dish_id`                     varchar(20)                   NOT NULL DEFAULT '', 
`cardid`                      varchar(32)                   NOT NULL DEFAULT '', 
`card_level`                  int                           NOT NULL DEFAULT 0, 
`card_group`                  int                           NOT NULL DEFAULT 0, 
`pid`                         varchar(20)                   NOT NULL DEFAULT '', 
`pname`                       varchar(64)                   NOT NULL DEFAULT '', 
`weight`                      int                           NOT NULL DEFAULT 0, 
`part`					      int							NOT NULL DEFAULT 0,
`price`                       int                           NOT NULL DEFAULT 0, 
`amt`                         int                           NULL DEFAULT 0,
`taketime`                    datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP, 
`tick`                        bigint                        NOT NULL DEFAULT 0, 
`create_date`                 datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP, 
`mode`                        int                           NOT NULL DEFAULT 0, 
`finish_date`                 datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP, 
`major`                       bigint                        NOT NULL DEFAULT 0, 
`cate`                        int                           NULL DEFAULT 0, 
`stall`                       int                           NULL DEFAULT 0, 
CONSTRAINT PK_GAGE_TRADE_ARCHIV PRIMARY KEY (`seq`)
);

call del_idx('gage_trade_archiv','IX_gage_trade_archiv_1');  
ALTER TABLE gage_trade_archiv ADD INDEX IX_gage_trade_archiv_1 (TICK ASC);



CREATE TABLE if not exists `gage_trade_fail` ( 
`seq`                         bigint                        NOT NULL, 
`shop`                        int                           NOT NULL DEFAULT 0, 
`term`                        int                           NOT NULL DEFAULT 0, 
`dish_id`                     varchar(20)                   NOT NULL DEFAULT '', 
`cardid`                      varchar(32)                   NOT NULL DEFAULT '', 
`card_level`                  int                           NOT NULL DEFAULT 0, 
`card_group`                  int                           NOT NULL DEFAULT 0, 
`pid`                         varchar(20)                   NOT NULL DEFAULT '', 
`pname`                       varchar(64)                   NOT NULL DEFAULT '', 
`weight`                      int                           NOT NULL DEFAULT 0, 
`part`					      int							NOT NULL DEFAULT 0,
`price`                       int                           NOT NULL DEFAULT 0, 
`amt`                         int                           NULL DEFAULT 0,
`taketime`                    datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP, 
`tick`                        bigint                        NOT NULL DEFAULT 0, 
`create_date`                 datetime                      NOT NULL DEFAULT CURRENT_TIMESTAMP, 
`cate`                        int                           NULL DEFAULT 0, 
`stall`                       int                           NULL DEFAULT 0, 
CONSTRAINT PK_GAGE_TRADE_FAIL PRIMARY KEY (`seq`)
);

call del_idx('gage_trade_fail','IX_gage_trade_fail_1');  
ALTER TABLE gage_trade_fail ADD INDEX IX_gage_trade_fail_1 (TICK ASC,taketime ASC);

call alter_table_add('dish_trade', 'properties', 'varchar(1000) not null default ""');
call alter_table_add('dish_trade_details', 'weight', 'int not null default 0');

-- 掌上通微信通知表
CREATE TABLE if not exists  `mobile_notice` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `card_id` varchar(30) NOT NULL,
  `data` varchar(1000) NOT NULL,
  `create_date` datetime NOT NULL DEFAULT now(),
  PRIMARY KEY (`id`)
)   DEFAULT CHARSET=utf8;
-- 掌上通微信通知存档表
CREATE TABLE if not exists  `mobile_notice_archiv` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `notice_id` int(11) NOT NULL DEFAULT '0',
  `card_id` varchar(30) NOT NULL,
  `state` int(11) NOT NULL DEFAULT '0',
  `data` varchar(1000) NOT NULL,
  `notice_date` datetime NOT NULL,
  `remark` varchar(255) NOT NULL,
  `create_date` datetime NOT NULL DEFAULT now(),
  PRIMARY KEY (`id`)
)   DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `gage_eer` ( 
`id`                          int                           NOT NULL AUTO_INCREMENT, 
`age`                         int                           NOT NULL DEFAULT 0, 
`workload`                    int                           NOT NULL DEFAULT 0, 
`sex`                         int                           NOT NULL DEFAULT 0, 
`energy`                      int                           NOT NULL DEFAULT 0, 
CONSTRAINT PK_GAGE_EER PRIMARY KEY (`id`)
);

truncate table gage_eer;
INSERT INTO `gage_eer` VALUES ('1', '1', '2', '0', '900');
INSERT INTO `gage_eer` VALUES ('2', '1', '2', '1', '800');
INSERT INTO `gage_eer` VALUES ('3', '2', '2', '0', '1100');
INSERT INTO `gage_eer` VALUES ('4', '2', '2', '1', '1000');
INSERT INTO `gage_eer` VALUES ('5', '3', '2', '0', '1250');
INSERT INTO `gage_eer` VALUES ('6', '3', '2', '1', '1200');
INSERT INTO `gage_eer` VALUES ('7', '4', '2', '0', '1300');
INSERT INTO `gage_eer` VALUES ('8', '4', '2', '1', '1250');
INSERT INTO `gage_eer` VALUES ('9', '5', '2', '0', '1400');
INSERT INTO `gage_eer` VALUES ('10', '5', '2', '1', '1300');
INSERT INTO `gage_eer` VALUES ('11', '6', '1', '0', '1400');
INSERT INTO `gage_eer` VALUES ('12', '6', '1', '1', '1250');
INSERT INTO `gage_eer` VALUES ('13', '6', '2', '0', '1600');
INSERT INTO `gage_eer` VALUES ('14', '6', '2', '1', '1450');
INSERT INTO `gage_eer` VALUES ('15', '6', '3', '0', '1800');
INSERT INTO `gage_eer` VALUES ('16', '6', '3', '1', '1650');
INSERT INTO `gage_eer` VALUES ('17', '7', '1', '0', '1500');
INSERT INTO `gage_eer` VALUES ('18', '7', '1', '1', '1350');
INSERT INTO `gage_eer` VALUES ('19', '7', '2', '0', '1700');
INSERT INTO `gage_eer` VALUES ('20', '7', '2', '1', '1550');
INSERT INTO `gage_eer` VALUES ('21', '7', '3', '0', '1900');
INSERT INTO `gage_eer` VALUES ('22', '7', '3', '1', '1750');
INSERT INTO `gage_eer` VALUES ('23', '8', '1', '0', '1650');
INSERT INTO `gage_eer` VALUES ('24', '8', '1', '1', '1450');
INSERT INTO `gage_eer` VALUES ('25', '8', '2', '0', '1850');
INSERT INTO `gage_eer` VALUES ('26', '8', '2', '1', '1700');
INSERT INTO `gage_eer` VALUES ('27', '8', '3', '0', '2100');
INSERT INTO `gage_eer` VALUES ('28', '8', '3', '1', '1900');
INSERT INTO `gage_eer` VALUES ('29', '9', '1', '0', '1750');
INSERT INTO `gage_eer` VALUES ('30', '9', '1', '1', '1550');
INSERT INTO `gage_eer` VALUES ('31', '9', '2', '0', '2000');
INSERT INTO `gage_eer` VALUES ('32', '9', '2', '1', '1800');
INSERT INTO `gage_eer` VALUES ('33', '9', '3', '0', '2250');
INSERT INTO `gage_eer` VALUES ('34', '9', '3', '1', '2000');
INSERT INTO `gage_eer` VALUES ('35', '10', '1', '0', '1800');
INSERT INTO `gage_eer` VALUES ('36', '10', '1', '1', '1650');
INSERT INTO `gage_eer` VALUES ('37', '10', '2', '0', '2050');
INSERT INTO `gage_eer` VALUES ('38', '10', '2', '1', '1900');
INSERT INTO `gage_eer` VALUES ('39', '10', '3', '0', '2300');
INSERT INTO `gage_eer` VALUES ('40', '10', '3', '1', '2150');
INSERT INTO `gage_eer` VALUES ('41', '11', '1', '0', '2050');
INSERT INTO `gage_eer` VALUES ('42', '11', '1', '1', '1800');
INSERT INTO `gage_eer` VALUES ('43', '11', '2', '0', '2350');
INSERT INTO `gage_eer` VALUES ('44', '11', '2', '1', '2050');
INSERT INTO `gage_eer` VALUES ('45', '11', '3', '0', '2600');
INSERT INTO `gage_eer` VALUES ('46', '11', '3', '1', '2300');
INSERT INTO `gage_eer` VALUES ('47', '14', '1', '0', '2500');
INSERT INTO `gage_eer` VALUES ('48', '14', '1', '1', '2000');
INSERT INTO `gage_eer` VALUES ('49', '14', '2', '0', '2850');
INSERT INTO `gage_eer` VALUES ('50', '14', '2', '1', '2300');
INSERT INTO `gage_eer` VALUES ('51', '14', '3', '0', '3200');
INSERT INTO `gage_eer` VALUES ('52', '14', '3', '1', '2550');
INSERT INTO `gage_eer` VALUES ('53', '18', '1', '0', '2250');
INSERT INTO `gage_eer` VALUES ('54', '18', '1', '1', '1800');
INSERT INTO `gage_eer` VALUES ('55', '18', '2', '0', '2600');
INSERT INTO `gage_eer` VALUES ('56', '18', '2', '1', '2100');
INSERT INTO `gage_eer` VALUES ('57', '18', '3', '0', '3000');
INSERT INTO `gage_eer` VALUES ('58', '18', '3', '1', '2400');
INSERT INTO `gage_eer` VALUES ('59', '50', '1', '0', '2100');
INSERT INTO `gage_eer` VALUES ('60', '50', '1', '1', '1750');
INSERT INTO `gage_eer` VALUES ('61', '50', '2', '0', '2450');
INSERT INTO `gage_eer` VALUES ('62', '50', '2', '1', '2050');
INSERT INTO `gage_eer` VALUES ('63', '50', '3', '0', '2800');
INSERT INTO `gage_eer` VALUES ('64', '50', '3', '1', '2350');
INSERT INTO `gage_eer` VALUES ('65', '65', '1', '0', '2050');
INSERT INTO `gage_eer` VALUES ('66', '65', '1', '1', '1700');
INSERT INTO `gage_eer` VALUES ('67', '65', '2', '0', '2350');
INSERT INTO `gage_eer` VALUES ('68', '65', '2', '1', '1950');
INSERT INTO `gage_eer` VALUES ('69', '80', '1', '0', '1900');
INSERT INTO `gage_eer` VALUES ('70', '80', '1', '1', '1500');
INSERT INTO `gage_eer` VALUES ('71', '80', '2', '0', '2200');
INSERT INTO `gage_eer` VALUES ('72', '80', '2', '1', '1750');

INSERT INTO config(`key`,val) SELECT 'gage_module_title1','智能托盘机' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_module_title1');-- 智能托盘机标题
INSERT INTO config(`key`,val) SELECT 'gage_module_title2','智能计量选餐台' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_module_title2');-- 智能计量选餐台标题
INSERT INTO config(`key`,val) SELECT 'gage_logon_psw','85330909' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_logon_psw');-- 智能托盘机和智能计量选餐台的设置模块登录密码定义
INSERT INTO config(`key`,val) SELECT 'gage_take_meal_time','1000' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_take_meal_time');-- 智能计量选餐台，人员在取餐时在规定时间内检测不到餐盘视为已离开当前取菜槽，单位为豪秒
INSERT INTO config(`key`,val) SELECT 'gage_query_dish_time','10' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_query_dish_time');-- 智能计量选餐台定时获取菜品信息功能，单位为分钟，默认10分钟
INSERT into config(`key`,val) SELECT 'gage_energy_scale','1500' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_energy_scale'); -- 取餐台刻度值

INSERT into config(`key`,val) SELECT 'gage_color1_back','#2800DB63' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_color1_back'); -- ,'绿色天然背景色'
INSERT into config(`key`,val) SELECT 'gage_color1_text','#00DB63' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_color1_text'); -- '绿色天然字体色'
INSERT into config(`key`,val) SELECT 'gage_color2_back','#28F2C600' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_color2_back'); -- '多多益善背景色'
INSERT into config(`key`,val) SELECT 'gage_color2_text','#F2C600' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_color2_text'); -- '多多益善字体色'
INSERT into config(`key`,val) SELECT 'gage_color3_back','#28F10027' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_color3_back'); -- '健康控制背景色'
INSERT into config(`key`,val) SELECT 'gage_color3_text','#F10027' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_color3_text');-- '健康控制字体色'

INSERT into config(`key`,val) SELECT 'gage_people_suggest1','推荐食用' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_suggest1'); -- '提示1文字'
INSERT into config(`key`,val) SELECT 'gage_people_color1_back','#00DB63' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_color1_back'); -- '推荐食用背景色'
INSERT into config(`key`,val) SELECT 'gage_people_color1_text','#042E21' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_color1_text'); -- '推荐食用字体色'
INSERT into config(`key`,val) SELECT 'gage_people_suggest2','适量食用' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_suggest2'); -- '提示2文字'
INSERT into config(`key`,val) SELECT 'gage_people_color2_back','#F2C600' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_color2_back'); -- '适量食用背景色'
INSERT into config(`key`,val) SELECT 'gage_people_color2_text','#042E21' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_color2_text');-- '适量食用字体色'
INSERT into config(`key`,val) SELECT 'gage_people_suggest3','谨慎食用' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_suggest3'); -- '提示3文字'
INSERT into config(`key`,val) SELECT 'gage_people_color3_back','#F10027' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_color3_back'); -- '谨慎食用背景色'
INSERT into config(`key`,val) SELECT 'gage_people_color3_text','#FFFFFF' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_people_color3_text');-- '谨慎食用字体色'

INSERT into config(`key`,val) SELECT 'gage_alarm_plate','1000' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_alarm_plate'); -- '选餐台菜品剩余重量预警'
INSERT into config(`key`,val) SELECT 'gage_alarm_tray','10' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_alarm_tray'); -- '托盘机剩余托盘数预警'
INSERT into config(`key`,val) SELECT 'gage_tray_capacity','80' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_tray_capacity'); -- '托盘机默认容量'
INSERT into config(`key`,val) SELECT 'gage_tray_timeinterval','30' FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM config WHERE `key` = 'gage_tray_timeinterval'); -- 'XX分钟内,计算托盘机预计可用时间'
