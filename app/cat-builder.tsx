"use client";

import { ChangeEvent, type CSSProperties, useMemo, useState } from "react";

const slots = ["正面", "左侧", "右侧", "背面"];
const bodyTypes = ["普通短毛", "圆胖短毛", "修长短毛", "短腿猫", "长毛毛绒版"];

type PhotoState = { name: string; url: string };

export function CatBuilder() {
  const [photos, setPhotos] = useState<Array<PhotoState | null>>(slots.map(() => null));
  const [bodyType, setBodyType] = useState(bodyTypes[0]);
  const [shape, setShape] = useState({ weight: 52, face: 50, ears: 48, legs: 55 });
  const photoCount = photos.filter(Boolean).length;
  const ready = photoCount >= 3;
  const catStyle = useMemo(() => ({
    "--cat-scale-x": `${0.78 + shape.weight / 250}`,
    "--cat-head-scale": `${0.82 + shape.face / 280}`,
    "--cat-ear-scale": `${0.72 + shape.ears / 180}`,
    "--cat-leg-height": `${42 + shape.legs * 0.35}px`,
  }) as CSSProperties, [shape]);

  function addPhoto(index: number, event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    setPhotos((current) => {
      const next = [...current];
      if (next[index]?.url) URL.revokeObjectURL(next[index]!.url);
      next[index] = { name: file.name, url: URL.createObjectURL(file) };
      return next;
    });
  }

  return (
    <section className="builder-section" id="builder">
      <div className="builder-heading">
        <div><p className="eyebrow">交互原型</p><h2>先看看你的猫适不适合生成</h2></div>
        <p>照片只在当前浏览器本地预览，本版本不会上传或保存。</p>
      </div>

      <div className="builder-shell">
        <div className="upload-panel">
          <div className="panel-title">
            <span>1</span><div><h3>添加多角度照片</h3><p>至少 3 张，推荐 4 张</p></div><b>{photoCount}/4</b>
          </div>
          <div className="upload-grid">
            {slots.map((slot, index) => (
              <label className={`upload-slot ${photos[index] ? "has-photo" : ""}`} key={slot}>
                <input type="file" accept="image/png,image/jpeg,image/webp" onChange={(event) => addPhoto(index, event)} />
                {photos[index] ? (
                  <><img src={photos[index]!.url} alt={`${slot}猫咪照片预览`} /><span>{slot} · 更换</span></>
                ) : (
                  <><i>＋</i><strong>{slot}</strong><small>JPG / PNG / WebP</small></>
                )}
              </label>
            ))}
          </div>
          <p className="upload-tip">光线均匀、猫咪完整入镜、没有衣服或遮挡时效果最好。</p>
        </div>

        <div className="morph-panel">
          <div className="panel-title">
            <span>2</span><div><h3>选择体型并微调</h3><p>这些调整不消耗修正次数</p></div>
          </div>
          <div className="body-types" role="radiogroup" aria-label="标准猫体型">
            {bodyTypes.map((type) => (
              <button className={bodyType === type ? "selected" : ""} key={type} onClick={() => setBodyType(type)} role="radio" aria-checked={bodyType === type}>
                {type}
              </button>
            ))}
          </div>
          <div className="morph-controls">
            {([["weight", "胖瘦"], ["face", "脸宽"], ["ears", "耳朵"], ["legs", "腿长"]] as const).map(([key, label]) => (
              <label key={key}>
                <span>{label}</span>
                <input type="range" min="0" max="100" value={shape[key]} onChange={(event) => setShape((current) => ({ ...current, [key]: Number(event.target.value) }))} />
                <output>{shape[key]}</output>
              </label>
            ))}
          </div>
        </div>

        <div className="preview-panel">
          <div className="panel-title"><span>3</span><div><h3>静态体型预览</h3><p>{bodyType}</p></div></div>
          <div className="preview-stage" style={catStyle}>
            <div className="preview-cat" aria-label="猫咪体型参数预览">
              <div className="preview-tail" /><div className="preview-body" />
              <div className="preview-head">
                <i className="preview-ear left" /><i className="preview-ear right" />
                <i className="preview-eye left" /><i className="preview-eye right" />
              </div>
              <div className="preview-leg left" /><div className="preview-leg right" />
            </div>
            <div className="stage-shadow" />
          </div>
          <button className="preview-button" disabled={!ready}>
            {ready ? "照片已就绪 · 生成服务待接入" : `还需 ${Math.max(0, 3 - photoCount)} 张照片`}
          </button>
          <p className="prototype-note">这是产品流程原型，不会收费或调用云端 AI。</p>
        </div>
      </div>
    </section>
  );
}
