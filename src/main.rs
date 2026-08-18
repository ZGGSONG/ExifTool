#![cfg_attr(
    all(target_os = "windows", not(debug_assertions)),
    windows_subsystem = "windows"
)]

//! GPUI desktop entry point for ExifTool.

use std::path::PathBuf;

use exif_tool::{PhotoRenameResult, PhotoRenameService, RenameStatus};
use gpui::{
    App, Application, Bounds, Context, ExternalPaths, IntoElement, Render, Rgba, Window,
    WindowAppearance, WindowBounds, WindowOptions, div, prelude::*, px, rgb, size,
};

const WINDOW_WIDTH: f32 = 900.0;
const WINDOW_HEIGHT: f32 = 560.0;
const MIN_WINDOW_WIDTH: f32 = 720.0;
const MIN_WINDOW_HEIGHT: f32 = 480.0;

struct ExifToolView {
    is_processing: bool,
    status_message: String,
    results: Vec<PhotoRenameResult>,
}

impl ExifToolView {
    fn new(window: &mut Window, cx: &mut Context<Self>) -> Self {
        window.set_window_title("ExifTool");
        cx.observe_window_appearance(window, |_, _, cx| cx.notify())
            .detach();

        Self {
            is_processing: false,
            status_message: "拖拽图片或 MOV 到窗口中".to_owned(),
            results: Vec::new(),
        }
    }

    fn start_batch(&mut self, paths: Vec<PathBuf>, cx: &mut Context<Self>) {
        if self.is_processing {
            return;
        }

        let paths: Vec<_> = paths
            .into_iter()
            .filter(|path| !path.as_os_str().is_empty())
            .collect();
        if paths.is_empty() {
            self.status_message = "没有找到本地文件".to_owned();
            cx.notify();
            return;
        }

        self.is_processing = true;
        self.results.clear();
        self.status_message = format!("正在处理 {} 个文件...", paths.len());
        cx.notify();

        let rename_task = cx
            .background_executor()
            .spawn(async move { PhotoRenameService::default().rename_files(paths) });

        cx.spawn(async move |view, cx| {
            let results = rename_task.await;
            let _ = view.update(cx, |view, cx| view.finish_batch(results, cx));
        })
        .detach();
    }

    fn finish_batch(&mut self, results: Vec<PhotoRenameResult>, cx: &mut Context<Self>) {
        let succeeded = results
            .iter()
            .filter(|result| {
                matches!(
                    result.status,
                    RenameStatus::Renamed | RenameStatus::AlreadyNamed
                )
            })
            .count();
        let failed = results.len() - succeeded;

        self.results = results;
        self.is_processing = false;
        self.status_message = if failed == 0 {
            format!("完成：{succeeded} 个文件已处理")
        } else {
            format!("完成：{succeeded} 个成功，{failed} 个失败")
        };
        cx.notify();
    }

    fn result_row(result: &PhotoRenameResult, colors: ThemeColors) -> impl IntoElement {
        div()
            .flex()
            .items_center()
            .min_h(px(54.0))
            .px(px(18.0))
            .gap(px(12.0))
            .border_b_1()
            .border_color(colors.panel_border)
            .text_sm()
            .child(table_cell(result.source_file_name(), colors.primary_text).flex_1())
            .child(table_cell(result.target_file_name(), colors.primary_text).flex_1())
            .child(
                table_cell(result.status_text(), colors.primary_text)
                    .w(px(140.0))
                    .flex_none(),
            )
            .child(
                table_cell(result.timestamp_source_text(), colors.secondary_text)
                    .w(px(150.0))
                    .flex_none(),
            )
    }
}

impl Render for ExifToolView {
    fn render(&mut self, window: &mut Window, cx: &mut Context<Self>) -> impl IntoElement {
        let colors = ThemeColors::for_appearance(window.appearance());

        div()
            .id("app-root")
            .size_full()
            .flex()
            .flex_col()
            .gap(px(18.0))
            .p(px(24.0))
            .bg(colors.window_background)
            .text_color(colors.primary_text)
            .on_drop(cx.listener(
                |view, paths: &ExternalPaths, _window, cx| {
                    view.start_batch(paths.paths().to_vec(), cx);
                },
            ))
            .child(
                div()
                    .min_h(px(156.0))
                    .flex_none()
                    .flex()
                    .flex_col()
                    .items_center()
                    .justify_center()
                    .gap(px(10.0))
                    .p(px(28.0))
                    .rounded(px(8.0))
                    .border_1()
                    .border_color(colors.drop_zone_border)
                    .bg(colors.drop_zone_background)
                    .child(
                        div()
                            .text_2xl()
                            .font_weight(gpui::FontWeight::SEMIBOLD)
                            .text_center()
                            .child("拖拽图片或 MOV 到窗口中"),
                    )
                    .child(
                        div()
                            .max_w(px(620.0))
                            .text_sm()
                            .text_center()
                            .text_color(colors.secondary_text)
                            .child(
                                "按拍摄时间原地重命名；没有元数据时间时使用创建时间；同名目标会添加序号后缀。",
                            ),
                    )
                    .when(self.is_processing, |drop_zone| {
                        drop_zone.child(
                            div()
                                .w(px(260.0))
                                .h(px(4.0))
                                .rounded_full()
                                .bg(colors.progress_track)
                                .child(
                                    div()
                                        .w(px(130.0))
                                        .h_full()
                                        .rounded_full()
                                        .bg(colors.progress_fill),
                                ),
                        )
                    }),
            )
            .child(
                div()
                    .flex_1()
                    .min_h_0()
                    .flex()
                    .flex_col()
                    .overflow_hidden()
                    .rounded(px(8.0))
                    .border_1()
                    .border_color(colors.panel_border)
                    .bg(colors.panel_background)
                    .child(table_header(colors))
                    .child(
                        div()
                            .id("results")
                            .flex_1()
                            .min_h_0()
                            .overflow_y_scroll()
                            .children(
                                self.results
                                    .iter()
                                    .map(|result| Self::result_row(result, colors)),
                            ),
                    ),
            )
            .child(
                div()
                    .flex_none()
                    .h(px(20.0))
                    .text_sm()
                    .text_color(colors.status_text)
                    .whitespace_nowrap()
                    .text_ellipsis()
                    .child(self.status_message.clone()),
            )
    }
}

fn table_header(colors: ThemeColors) -> impl IntoElement {
    div()
        .flex_none()
        .flex()
        .items_center()
        .gap(px(12.0))
        .px(px(18.0))
        .py(px(12.0))
        .bg(colors.table_header_background)
        .text_sm()
        .font_weight(gpui::FontWeight::SEMIBOLD)
        .text_color(colors.table_header_text)
        .child(table_header_cell("原文件").flex_1())
        .child(table_header_cell("新文件").flex_1())
        .child(table_header_cell("状态").w(px(140.0)).flex_none())
        .child(table_header_cell("时间来源").w(px(150.0)).flex_none())
}

fn table_header_cell(text: &'static str) -> gpui::Div {
    div()
        .min_w_0()
        .whitespace_nowrap()
        .text_ellipsis()
        .child(text)
}

fn table_cell(text: impl Into<gpui::SharedString>, color: Rgba) -> gpui::Div {
    div()
        .min_w_0()
        .whitespace_nowrap()
        .text_ellipsis()
        .text_color(color)
        .child(text.into())
}

#[derive(Clone, Copy)]
struct ThemeColors {
    window_background: Rgba,
    drop_zone_background: Rgba,
    drop_zone_border: Rgba,
    primary_text: Rgba,
    secondary_text: Rgba,
    panel_background: Rgba,
    panel_border: Rgba,
    table_header_background: Rgba,
    table_header_text: Rgba,
    status_text: Rgba,
    progress_track: Rgba,
    progress_fill: Rgba,
}

impl ThemeColors {
    fn for_appearance(appearance: WindowAppearance) -> Self {
        match appearance {
            WindowAppearance::Dark | WindowAppearance::VibrantDark => Self {
                window_background: rgb(0x0f1419),
                drop_zone_background: rgb(0x17212b),
                drop_zone_border: rgb(0x3e5163),
                primary_text: rgb(0xe8eef4),
                secondary_text: rgb(0xb6c5d4),
                panel_background: rgb(0x151b22),
                panel_border: rgb(0x334252),
                table_header_background: rgb(0x1f2a35),
                table_header_text: rgb(0xe8eef4),
                status_text: rgb(0x8da7bf),
                progress_track: rgb(0x334252),
                progress_fill: rgb(0x5794cc),
            },
            WindowAppearance::Light | WindowAppearance::VibrantLight => Self {
                window_background: rgb(0xffffff),
                drop_zone_background: rgb(0xf7fafc),
                drop_zone_border: rgb(0x8aa0b5),
                primary_text: rgb(0x17212b),
                secondary_text: rgb(0x415466),
                panel_background: rgb(0xffffff),
                panel_border: rgb(0xd0d7de),
                table_header_background: rgb(0xedf2f7),
                table_header_text: rgb(0x17212b),
                status_text: rgb(0x415466),
                progress_track: rgb(0xdbe4ec),
                progress_fill: rgb(0x3979b8),
            },
        }
    }
}

fn main() {
    Application::new().run(|cx: &mut App| {
        let bounds = Bounds::centered(None, size(px(WINDOW_WIDTH), px(WINDOW_HEIGHT)), cx);
        cx.open_window(
            WindowOptions {
                window_bounds: Some(WindowBounds::Windowed(bounds)),
                window_min_size: Some(size(px(MIN_WINDOW_WIDTH), px(MIN_WINDOW_HEIGHT))),
                app_id: Some("com.exiftool.desktop".to_owned()),
                ..Default::default()
            },
            |window, cx| cx.new(|cx| ExifToolView::new(window, cx)),
        )
        .expect("无法创建 ExifTool 主窗口");
        cx.activate(true);
    });
}
